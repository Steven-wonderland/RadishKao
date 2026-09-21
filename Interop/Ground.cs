namespace CatPet.Interop;

/// <summary>Where the cat is allowed to walk, in physical screen pixels.</summary>
/// <param name="Left">Leftmost x for the window.</param>
/// <param name="Right">Rightmost x for the window's left edge.</param>
/// <param name="FloorY">Screen y the cat's paws rest on.</param>
internal readonly record struct Ground(int Left, int Right, int FloorY)
{
    public int Span => Math.Max(1, Right - Left);
}

internal static class Taskbar
{
    /// <summary>
    /// Works out the strip of screen the cat lives on. With the usual bottom
    /// taskbar the cat stands on the taskbar's top edge; if the taskbar is
    /// docked to a side or the top, it falls back to the bottom of the work
    /// area so the cat still walks along the bottom of the screen.
    /// </summary>
    public static Ground Resolve(int catWidthPx, int footOffsetPx)
    {
        if (TryGetTaskbar(out var rc, out var edge) && edge == NativeMethods.ABE_BOTTOM)
        {
            return new Ground(
                rc.Left,
                Math.Max(rc.Left, rc.Right - catWidthPx),
                rc.Top + footOffsetPx);
        }

        var work = PrimaryWorkArea();
        return new Ground(
            work.Left,
            Math.Max(work.Left, work.Right - catWidthPx),
            work.Bottom + footOffsetPx);
    }

    private static bool TryGetTaskbar(out NativeMethods.RECT rc, out uint edge)
    {
        var data = new NativeMethods.APPBARDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>()
        };

        if (NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETTASKBARPOS, ref data) != IntPtr.Zero
            && data.rc.Width > 0 && data.rc.Height > 0)
        {
            rc = data.rc;
            edge = data.uEdge;
            return true;
        }

        rc = default;
        edge = NativeMethods.ABE_BOTTOM;
        return false;
    }

    private static NativeMethods.RECT PrimaryWorkArea()
    {
        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        var monitor = NativeMethods.MonitorFromPoint(origin, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };

        if (NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return info.rcWork;
        }

        // Last resort: assume a single 1920x1080 screen rather than crashing.
        return new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    }
}
