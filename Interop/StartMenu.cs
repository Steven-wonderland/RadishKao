using System.Runtime.InteropServices;

namespace CatPet.Interop;

/// <summary>Opens the Start menu, as if someone had pressed the button.</summary>
internal static class StartMenu
{
    /// <summary>
    /// Asks the shell to show the Start menu.
    ///
    /// The default route posts WM_SYSCOMMAND/SC_TASKLIST to the taskbar, which
    /// has meant "open Start" since Windows 95 and touches no keyboard state at
    /// all. Synthesising the Windows key works too, but if the cat swats while
    /// a modifier is held down it turns into Win+Shift+whatever, so it is opt-in.
    /// </summary>
    /// <returns>Null on success, otherwise why it failed.</returns>
    public static string? Open(bool useWindowsKey)
    {
        return useWindowsKey ? PressWindowsKey() : PostToShell();
    }

    private static string? PostToShell()
    {
        var tray = NativeMethods.FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero)
        {
            return "找不到工作列視窗";
        }

        return NativeMethods.PostMessage(
            tray,
            NativeMethods.WM_SYSCOMMAND,
            new IntPtr(NativeMethods.SC_TASKLIST),
            IntPtr.Zero)
            ? null
            : "工作列沒有回應";
    }

    private static string? PressWindowsKey()
    {
        var keys = new NativeMethods.INPUT[2];

        keys[0].type = NativeMethods.INPUT_KEYBOARD;
        keys[0].U.ki.wVk = NativeMethods.VK_LWIN;

        keys[1].type = NativeMethods.INPUT_KEYBOARD;
        keys[1].U.ki.wVk = NativeMethods.VK_LWIN;
        keys[1].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        var size = Marshal.SizeOf<NativeMethods.INPUT>();
        var sent = NativeMethods.SendInput((uint)keys.Length, keys, size);

        // A partial send would leave the key stuck down, so put it back up.
        if (sent == 1)
        {
            NativeMethods.SendInput(1, [keys[1]], size);
            return "Windows 鍵只送出一半，已補上放開";
        }

        return sent == keys.Length ? null : "送不出 Windows 鍵";
    }
}
