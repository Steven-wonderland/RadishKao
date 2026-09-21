namespace CatPet.Sprites;

/// <summary>
/// A named slice of one sprite-sheet row.
/// </summary>
/// <param name="Name">Key used by the behaviour table and by config.json.</param>
/// <param name="Row">Sprite-sheet row.</param>
/// <param name="Start">First column of the slice.</param>
/// <param name="Count">How many columns the slice covers.</param>
/// <param name="FrameMs">How long each frame is held.</param>
/// <param name="Loop">Restart at the beginning instead of stopping on the last frame.</param>
/// <param name="PingPong">Play forwards then back again (n, n-1, ... 0) each cycle.</param>
/// <param name="Baseline">
/// Cell-local y of the lowest paw in this clip, which is what gets lined up with
/// the taskbar. It is per clip because the rows are not drawn to a shared floor:
/// the standing poses reach y=200 but the original running ones stop at y=185,
/// so sharing one number leaves the cat hovering while it runs.
/// </param>
/// <param name="Reverse">Play the slice last frame first.</param>
/// <param name="Zoom">
/// Render-only size correction for art drawn at a different scale from the
/// main sheet. It does not affect the paw line or the window box, so the cat
/// still sits on the taskbar and the global size setting is untouched.
/// </param>
/// <param name="Sheet">Key from <see cref="ClipLibrary.Sheets"/> the row lives in.</param>
/// <param name="Frames">
/// Explicit column order, replacing <paramref name="Start"/> and
/// <paramref name="Count"/>. Use it to skip a frame or to repeat part of a
/// sequence; <paramref name="Count"/> is then ignored.
/// </param>
public sealed record Clip(
    string Name,
    int Row,
    int Start,
    int Count,
    double FrameMs,
    bool Loop,
    bool PingPong = false,
    double Baseline = Clip.DefaultBaseline,
    bool Reverse = false,
    double Zoom = 1.0,
    string Sheet = ClipLibrary.MainSheet,
    int[]? Frames = null)
{
    /// <summary>Frames in this clip once an explicit order is taken into account.</summary>
    public int Length => Frames?.Length ?? Count;

    /// <summary>Paw line shared by every standing, sitting and dozing pose.</summary>
    public const double DefaultBaseline = 200;

    /// <summary>Number of steps in one full cycle once ping-pong is expanded.</summary>
    public int StepCount => PingPong && Length > 1 ? Length * 2 - 2 : Length;

    /// <summary>Sprite-sheet column for a given step of the cycle.</summary>
    public int ColumnForStep(int step)
    {
        var length = Length;

        if (PingPong && length > 1 && step >= length)
        {
            step = length * 2 - 2 - step;
        }

        step = Math.Clamp(step, 0, length - 1);

        if (Reverse)
        {
            step = length - 1 - step;
        }

        return Frames is not null ? Frames[step] : Start + step;
    }
}
