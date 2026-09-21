namespace CatPet.Config;

/// <summary>
/// One thing the cat can do in response to a trigger. Every field is optional,
/// so an action can be purely cosmetic (just an animation and a line of text),
/// purely functional (just launch something), or both.
/// </summary>
public sealed class TriggerAction
{
    /// <summary>Clip name from ClipLibrary, e.g. "wave", "jump", "play".</summary>
    public string? Animation { get; set; }

    /// <summary>Text for the speech bubble. Omit for a silent reaction.</summary>
    public string? Say { get; set; }

    /// <summary>
    /// Program, document or URL to launch. Anything ShellExecute accepts:
    /// "notepad.exe", "https://example.com", "C:\work\daily.xlsx".
    /// </summary>
    public string? Run { get; set; }

    /// <summary>Command-line arguments for <see cref="Run"/>.</summary>
    public string? Args { get; set; }

    /// <summary>Working directory for <see cref="Run"/>.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Launch through the shell (needed for URLs and documents).</summary>
    public bool UseShellExecute { get; set; } = true;

    /// <summary>Relative chance of being picked when a trigger lists several actions.</summary>
    public double Weight { get; set; } = 1;
}
