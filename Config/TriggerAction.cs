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
    /// Small image shown to the left of the bubble text, so a notification can
    /// carry the logo of whatever sent it. Relative to the exe or absolute, and
    /// the path may contain a * wildcard: the newest match wins, which is how a
    /// versioned install folder keeps working after that app updates itself.
    /// </summary>
    public string? Icon { get; set; }

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

    /// <summary>
    /// How long this action's bubble stays up, overriding the global
    /// <c>bubbleSeconds</c>. A notification worth clicking needs longer than a
    /// reaction to a poke.
    /// </summary>
    public double? BubbleSeconds { get; set; }

    /// <summary>
    /// What clicking the cat does while this action's bubble is still up, in
    /// place of the usual click reaction: the bubble becomes a button. Armed
    /// when the bubble appears and disarmed when it fades or is used, so a
    /// click a moment too late is an ordinary click again.
    /// </summary>
    public TriggerAction? OnClick { get; set; }
}
