namespace CatPet.Interop;

/// <summary>
/// Parses a hotkey written the way a person would ("Ctrl+F10", "ctrl+shift+B")
/// and registers it system-wide against a window.
/// </summary>
internal sealed class HotKeyBinding : IDisposable
{
    private IntPtr _hwnd;
    private bool _registered;

    private HotKeyBinding(int id, string text)
    {
        Id = id;
        Text = text;
    }

    public int Id { get; }

    public string Text { get; }

    /// <summary>Set when registration failed, usually because another app owns the combo.</summary>
    public string? Error { get; private set; }

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= NativeMethods.MOD_CONTROL; break;
                case "alt": modifiers |= NativeMethods.MOD_ALT; break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT; break;
                case "win" or "cmd" or "meta": modifiers |= NativeMethods.MOD_WIN; break;
                default: return false;
            }
        }

        var key = parts[^1];

        // Keys covers F1-F24, A-Z, Space and friends; bare digits need the D prefix.
        if (!Enum.TryParse<System.Windows.Forms.Keys>(key, ignoreCase: true, out var parsed)
            && !(key.Length == 1 && char.IsAsciiDigit(key[0])
                 && Enum.TryParse($"D{key}", ignoreCase: true, out parsed)))
        {
            return false;
        }

        virtualKey = (uint)parsed;
        return virtualKey != 0;
    }

    public static HotKeyBinding Register(IntPtr hwnd, int id, string text)
    {
        var binding = new HotKeyBinding(id, text) { _hwnd = hwnd };

        if (!TryParse(text, out var modifiers, out var virtualKey))
        {
            binding.Error = $"看不懂的快捷鍵「{text}」";
            return binding;
        }

        // MOD_NOREPEAT stops a held-down combo firing over and over.
        if (NativeMethods.RegisterHotKey(hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey))
        {
            binding._registered = true;
        }
        else
        {
            binding.Error = $"{text} 已經被其他程式佔用了";
        }

        return binding;
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, Id);
            _registered = false;
        }
    }
}
