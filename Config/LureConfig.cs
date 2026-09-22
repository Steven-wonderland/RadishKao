namespace CatPet.Config;

/// <summary>
/// Something you can drop on screen for the cat to run after. A hotkey turns
/// the pointer into the item; clicking puts it down; the cat runs over, does
/// <see cref="ArriveAnimation"/> for a bit, then heads back to the taskbar.
/// </summary>
public sealed class LureConfig
{
    /// <summary>Used in messages and to keep hotkey ids stable.</summary>
    public string Name { get; set; } = "lure";

    /// <summary>System-wide hotkey, e.g. "Ctrl+F10".</summary>
    public string Hotkey { get; set; } = string.Empty;

    /// <summary>PNG for both the cursor and the thing left on screen.</summary>
    public string Image { get; set; } = string.Empty;

    /// <summary>On-screen height in device-independent pixels.</summary>
    public double DisplaySize { get; set; } = 30;

    /// <summary>
    /// Point in the 192x208 sprite cell that should end up on top of the item
    /// when the cat arrives. For the yarn ball this is where the ball sits in
    /// the "play" art, so the drawn ball lands exactly where the real one was.
    /// </summary>
    public double AnchorX { get; set; } = 140;

    public double AnchorY { get; set; } = 152;

    /// <summary>Clip played once the cat gets there.</summary>
    public string ArriveAnimation { get; set; } = "sit";

    /// <summary>How long the cat stays with it.</summary>
    public double ArriveSeconds { get; set; } = 3.5;

    /// <summary>
    /// True when the arrival art already draws the item (the "play" clip has
    /// its own ball), so the dropped one is hidden to avoid a duplicate.
    /// </summary>
    public bool HideOnArrive { get; set; }

    /// <summary>How much faster than <c>runSpeed</c> the cat charges over.</summary>
    public double SpeedMultiplier { get; set; } = 1.6;

    /// <summary>Line said when the item is put down.</summary>
    public string? Say { get; set; }

    /// <summary>Line said on arrival.</summary>
    public string? ArriveSay { get; set; }

    /// <summary>
    /// Line said once <see cref="ArriveSeconds"/> is up and the cat turns for
    /// home -- the "finished with it" beat, as opposed to
    /// <see cref="ArriveSay"/> which fires the moment it gets there.
    /// </summary>
    public string? FinishSay { get; set; }

    /// <summary>Hint shown across the screen while you are choosing a spot.</summary>
    public string? Hint { get; set; }
}
