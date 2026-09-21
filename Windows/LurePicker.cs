using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CatPet.Interop;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Cursor = System.Windows.Input.Cursor;
using FontFamily = System.Windows.Media.FontFamily;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CatPet.Windows;

/// <summary>
/// Covers the primary screen while you choose where to put something down.
/// The custom cursor is set on this window rather than swapped system-wide, so
/// nothing outside the app is changed and the normal pointer comes straight
/// back when the window closes — even if the app is killed.
/// </summary>
internal sealed class LurePicker : Window
{
    private const double TimeoutSeconds = 12;

    private readonly DispatcherTimer _timeout = new();
    private bool _settled;

    /// <summary>Chosen spot, in physical screen pixels.</summary>
    public event Action<double, double>? Picked;

    public event Action? Cancelled;

    public LurePicker(Cursor cursor, string hint)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Title = "CatPet picker";

        // Just enough tint to show a mode is active without hiding anything.
        Background = new SolidColorBrush(Color.FromArgb(0x16, 0, 0, 0));
        Cursor = cursor;
        ForceCursor = true;

        // The whole virtual desktop, not just the primary screen, so the click
        // is caught wherever the pointer happens to be on a multi-monitor setup.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Content = BuildHint(hint);

        _timeout.Interval = TimeSpan.FromSeconds(TimeoutSeconds);
        _timeout.Tick += (_, _) => Settle(cancelled: true);
        _timeout.Start();

        Loaded += (_, _) => { Activate(); Focus(); };
        MouseLeftButtonDown += OnLeftDown;
        MouseRightButtonDown += OnRightDown;
        KeyDown += OnKeyDown;
        Deactivated += (_, _) => Settle(cancelled: true);
        Closed += (_, _) => _timeout.Stop();
    }

    private static UIElement BuildHint(string hint)
    {
        var text = new TextBlock
        {
            Text = hint,
            FontFamily = new FontFamily("Microsoft JhengHei UI, Segoe UI"),
            FontSize = 14,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9, Color = Colors.Black },
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xB4, 0x1E, 0x1E, 0x22)),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 9, 18, 9),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Margin = new Thickness(0, 64, 0, 0),
            IsHitTestVisible = false,
            Child = text,
        };
    }

    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        // GetCursorPos is unambiguously physical pixels, unlike PointToScreen
        // once per-monitor DPI gets involved.
        NativeMethods.GetCursorPos(out var cursor);
        Settle(cancelled: false, cursor.X, cursor.Y);
        e.Handled = true;
    }

    private void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        Settle(cancelled: true);
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            Settle(cancelled: true);
            e.Handled = true;
        }
    }

    private void Settle(bool cancelled, double x = 0, double y = 0)
    {
        if (_settled)
        {
            return;
        }

        _settled = true;
        _timeout.Stop();
        Close();

        if (cancelled)
        {
            Cancelled?.Invoke();
        }
        else
        {
            Picked?.Invoke(x, y);
        }
    }

    /// <summary>Closes the overlay from outside, e.g. the hotkey pressed again.</summary>
    public void CancelFromOutside() => Settle(cancelled: true);
}
