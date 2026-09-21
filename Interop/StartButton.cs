using System.Windows.Automation;

namespace CatPet.Interop;

/// <summary>Where the Start button is, and how it was found.</summary>
/// <param name="Rect">Screen rectangle in physical pixels.</param>
/// <param name="Source">Which lookup succeeded, for diagnosing odd setups.</param>
/// <param name="OnScreen">
/// False when the rectangle sits outside the virtual desktop, which is what an
/// auto-hiding taskbar looks like while it is tucked away.
/// </param>
internal readonly record struct StartButtonInfo(
    NativeMethods.RECT Rect,
    string Source,
    bool OnScreen)
{
    public int CentreX => Rect.Left + Rect.Width / 2;

    public int CentreY => Rect.Top + Rect.Height / 2;
}

/// <summary>
/// Finds the Start button on the taskbar.
///
/// Windows 10 gives it a real window of its own (class "Start" inside
/// Shell_TrayWnd), which is a cheap lookup. Windows 11 rebuilt the taskbar in
/// XAML, so it is not a window any more and only UI Automation can see it. The
/// Win10 lookup is tried first and simply comes back empty on stock Win11,
/// which doubles as the version check — and on Win11 machines running a shell
/// replacement that restores the classic taskbar, it correctly wins again.
///
/// Deliberately tolerant of how different machines are set up:
///   - left-aligned or centred taskbar (Win11 moves the button as items come
///     and go, so the answer is re-queried rather than remembered for long)
///   - taskbar docked to any edge
///   - the button living on a secondary monitor's taskbar
///   - auto-hide, reported through <see cref="StartButtonInfo.OnScreen"/>
///   - any display language, because the match is on AutomationId, never Name
///
/// The UIA query costs tens of milliseconds and can block, so it never runs on
/// the UI thread. See <see cref="LocateAsync"/>.
/// </summary>
internal static class StartButton
{
    private static readonly object Gate = new();
    private static StartButtonInfo? _cached;
    private static DateTime _cachedAt = DateTime.MinValue;

    /// <summary>How long a located rectangle is trusted before re-querying.</summary>
    private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(3);

    /// <summary>Last known answer, without going and looking again.</summary>
    public static StartButtonInfo? Cached
    {
        get
        {
            lock (Gate)
            {
                return _cached;
            }
        }
    }

    /// <summary>
    /// Locates the button off the UI thread. Null means no known lookup found
    /// it, which is the honest answer on a shell this does not understand.
    /// </summary>
    public static Task<StartButtonInfo?> LocateAsync()
    {
        lock (Gate)
        {
            if (_cached is { } fresh && DateTime.UtcNow - _cachedAt < CacheLife)
            {
                return Task.FromResult<StartButtonInfo?>(fresh);
            }
        }

        return Task.Run(() =>
        {
            var found = Locate();

            if (found is not null)
            {
                lock (Gate)
                {
                    _cached = found;
                    _cachedAt = DateTime.UtcNow;
                }
            }

            return found;
        });
    }

    private enum Route
    {
        Unknown,
        Legacy,
        Automation,
    }

    private static Route _route = Route.Unknown;

    private static StartButtonInfo? Locate()
    {
        if (_route == Route.Unknown)
        {
            _route = Calibrate();
        }

        var found = _route == Route.Legacy
            ? LegacyWindow() ?? PrimaryTaskbar() ?? SecondaryTaskbars()
            : PrimaryTaskbar() ?? SecondaryTaskbars() ?? LegacyWindow();

        if (found is null)
        {
            // Shell may have been mid-restart; re-decide next time round.
            _route = Route.Unknown;
        }

        return found;
    }

    /// <summary>
    /// Decides once which lookup to lead with. Windows 11 keeps a legacy Start
    /// window alive for compatibility: on this build it tracks the real XAML
    /// button exactly, but that is not something to assume on every machine, so
    /// the cheap lookup is only trusted after it has been seen to agree with
    /// UI Automation.
    /// </summary>
    private static Route Calibrate()
    {
        var legacy = LegacyWindow();
        var automation = PrimaryTaskbar();

        if (legacy is not { } l)
        {
            return Route.Automation;
        }

        if (automation is not { } a)
        {
            return Route.Legacy;
        }

        var apart = Math.Abs(l.CentreX - a.CentreX) + Math.Abs(l.CentreY - a.CentreY);
        return apart <= 4 ? Route.Legacy : Route.Automation;
    }

    /// <summary>Windows 10, or Win11 with a classic-taskbar shell replacement.</summary>
    private static StartButtonInfo? LegacyWindow()
    {
        var tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
        {
            return null;
        }

        var start = NativeMethods.FindWindowEx(tray, IntPtr.Zero, "Start", null);
        if (start == IntPtr.Zero || !NativeMethods.GetWindowRect(start, out var rect))
        {
            return null;
        }

        return Wrap(rect, "Start 視窗 (Win10 式)");
    }

    private static StartButtonInfo? PrimaryTaskbar() =>
        FromAutomation(NativeMethods.FindWindow("Shell_TrayWnd", null), "UIA 主工作列 (Win11 式)");

    /// <summary>
    /// Some multi-monitor setups show Start on the secondary taskbars too; if
    /// the primary did not answer, any of those will do.
    /// </summary>
    private static StartButtonInfo? SecondaryTaskbars()
    {
        var hwnd = IntPtr.Zero;

        while (true)
        {
            hwnd = NativeMethods.FindWindowEx(IntPtr.Zero, hwnd, "Shell_SecondaryTrayWnd", null);
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            if (FromAutomation(hwnd, "UIA 副螢幕工作列") is { } hit)
            {
                return hit;
            }
        }
    }

    private static StartButtonInfo? FromAutomation(IntPtr trayHwnd, string source)
    {
        if (trayHwnd == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var taskbar = AutomationElement.FromHandle(trayHwnd);

            // AutomationId is stable across display languages; Name is not.
            var button = taskbar?.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "StartButton"));

            if (button is null)
            {
                return null;
            }

            var bounds = button.Current.BoundingRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0
                || double.IsInfinity(bounds.X) || double.IsNaN(bounds.X))
            {
                return null;
            }

            return Wrap(
                new NativeMethods.RECT
                {
                    Left = (int)Math.Round(bounds.Left),
                    Top = (int)Math.Round(bounds.Top),
                    Right = (int)Math.Round(bounds.Right),
                    Bottom = (int)Math.Round(bounds.Bottom),
                },
                source);
        }
        catch (Exception)
        {
            // UIA throws while the shell is restarting; that is just "not found".
            return null;
        }
    }

    private static StartButtonInfo? Wrap(NativeMethods.RECT rect, string source)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        var left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        var top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        var onScreen = rect.Right > left && rect.Left < left + width
                       && rect.Bottom > top && rect.Top < top + height;

        return new StartButtonInfo(rect, source, onScreen);
    }
}
