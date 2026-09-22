namespace CatPet.Sprites;

/// <summary>Where a group of rows lives on disk.</summary>
/// <param name="Key">Name clips refer to.</param>
/// <param name="Path">Relative to the exe, unless rooted.</param>
/// <param name="Columns">Frames across.</param>
/// <param name="Rows">Rows down.</param>
/// <param name="Required">A missing optional sheet just disables its clips.</param>
public sealed record SheetDef(string Key, string Path, int Columns, int Rows, bool Required = false);

/// <summary>
/// Names every animation and says which sheet and row it comes from.
///
/// The original 8x11 sheet holds:
///   0  idle (6)         1  run right (8)    2  run left (8)     3  wave (4, unused)
///   4  jump (5, unused) 5  sit/doze (8)     6  play ball (6)    7  sit idle (6)
///   8  groom (6, unused) 9 look around (8)  10 look around, mirrored (8)
///
/// Rows 3, 4 and 8 are deliberately not exposed; the art is still there, so
/// re-enabling one is a matter of adding its line back to <see cref="All"/>.
/// The later single-row strips each sit in their own file.
///
/// Every sheet is assumed to use the same 192x208 cell as the main one.
/// </summary>
public static class ClipLibrary
{
    public const string MainSheet = "main";

    public static readonly SheetDef[] Sheets =
    [
        new(MainSheet,  "Assets/cat-spritesheet.png", Columns: 8, Rows: 11, Required: true),
        new("frontback", "Assets/run-front-back.png",  Columns: 8, Rows: 1),
        new("doze",      "Assets/doze-stretch.png",    Columns: 8, Rows: 1),
        new("squirm",    "Assets/held-squirm.png",     Columns: 6, Rows: 1),
        new("tap",       "Assets/taskbar-tap.png",     Columns: 8, Rows: 1),

        // Assets/side-roll.png is still in the folder but no longer loaded;
        // add it back here together with the "roll" clip to re-enable it.
    ];

    /// <summary>
    /// Fallback paw line. Each clip carries its own <see cref="Clip.Baseline"/>;
    /// this is only for code that has no particular clip in hand.
    /// </summary>
    public const double BaselineY = Clip.DefaultBaseline;

    private static readonly Clip[] All =
    [
        new("idle",       Row: 0,  Start: 0, Count: 6, FrameMs: 200, Loop: true),

        // The side-on running rows are drawn 15px higher in their cells than the
        // standing art, so they carry their own paw line.
        new("run_right",  Row: 1,  Start: 0, Count: 8, FrameMs: 80,  Loop: true, Baseline: 185),
        new("run_left",   Row: 2,  Start: 0, Count: 8, FrameMs: 80,  Loop: true, Baseline: 186),
        new("walk_right", Row: 1,  Start: 0, Count: 8, FrameMs: 135, Loop: true, Baseline: 185),
        new("walk_left",  Row: 2,  Start: 0, Count: 8, FrameMs: 135, Loop: true, Baseline: 186),

        new("sleep_in",   Row: 5,  Start: 0, Count: 4, FrameMs: 260, Loop: false, Zoom: SleepZoom),
        new("sleep_deep", Row: 5,  Start: 3, Count: 2, FrameMs: 700, Loop: true, PingPong: true, Zoom: SleepZoom),
        new("sleep_out",  Row: 5,  Start: 5, Count: 3, FrameMs: 260, Loop: false, Zoom: SleepZoom),
        new("play",       Row: 6,  Start: 0, Count: 6, FrameMs: 130, Loop: true),
        new("sit",        Row: 7,  Start: 0, Count: 6, FrameMs: 260, Loop: true, Zoom: SitZoom),
        new("look_right", Row: 9,  Start: 0, Count: 8, FrameMs: 170, Loop: false),
        new("look_left",  Row: 10, Start: 0, Count: 8, FrameMs: 170, Loop: false),

        // Running into and out of the screen, for chasing something dropped
        // further up the display than the taskbar.
        new("run_away",   Row: 0, Start: 0, Count: 4, FrameMs: 85, Loop: true,
            Baseline: 201, Sheet: "frontback"),
        new("run_toward", Row: 0, Start: 4, Count: 4, FrameMs: 85, Loop: true,
            Baseline: 202, Sheet: "frontback"),

        // Lying flat on the taskbar. The strip runs
        //   0-1 breathing  2 head up  3 fold down  4 deep stretch
        //   5 sit with paw up  6 yawn  7 sitting alert
        // Frame 5 is skipped in both directions, and the stretch pulses on 3/4
        // a few times before the cat finishes getting up.
        new("lie_down",     Row: 0, Count: 0, Start: 0, FrameMs: 210, Loop: false,
            Baseline: 201, Sheet: "doze",
            Frames: [7, 6, 4, 3, 2]),
        new("doze",         Row: 0, Start: 0, Count: 2, FrameMs: 900, Loop: true,
            PingPong: true, Baseline: 201, Sheet: "doze"),
        new("wake_stretch", Row: 0, Count: 0, Start: 0, FrameMs: 380, Loop: false,
            Baseline: 201, Zoom: StretchZoom, Sheet: "doze",
            Frames: [2, 3, 4, 3, 4, 3, 4, 6, 7]),


        // Batting at something on the taskbar. The paw comes down on frames
        // 4-5; see TapPawX / TapContactFrame for lining the hit up.
        new("swat",   Row: 0, Start: 0, Count: 8, FrameMs: TapFrameMs, Loop: false,
            Baseline: 204, Sheet: "tap"),

        // Dangling from the cursor. Positioned by GripX/GripY rather than the
        // paw line, because the hand is drawn at the top of the cell.
        new("held",   Row: 0, Start: 0, Count: 6, FrameMs: 110, Loop: true,
            Baseline: 202, Zoom: HeldZoom, Sheet: "squirm"),
    ];

    /// <summary>
    /// Size corrections for the rows and strips whose art is drawn to a
    /// different scale than the standing pose. Nudge these if a pose still
    /// looks off; they are render-only and do not move the paw line.
    /// </summary>
    // Only the stretch needs help: lying flat is already drawn wide, so it
    // stays at 1.0 and the boost lands as the cat unfolds out of it.
    private const double StretchZoom = 1.06;

    // Row 5 is drawn to the same scale as the rest of the main sheet: its first
    // frame measures 137x194 with the paws on y=200, same as the standing rows.
    // It only looks smaller as the cat crouches, which is the animation. So no
    // correction -- an earlier 0.88 here shrank the whole sleep sequence by 12%.
    private const double SleepZoom = 1.0;

    // Row 7 is the one row drawn oversized: its head measures about 121px
    // against 104px standing (and 106px on row 6, which is the same sitting
    // pose at the right scale), so the cat visibly swells whenever it sits
    // idle. Scaling down by the head ratio puts it back on the same scale.
    private const double SitZoom = 0.86;

    // The held art is genuinely smaller: the hand is drawn above the cat, so
    // squeezing hand and cat together into one 192x208 cell leaves the cat at
    // about three quarters the size it is on sheets where it has the cell to
    // itself (head 79px against 104px standing). This scales it back up.
    private const double HeldZoom = 1.3;

    /// <summary>
    /// Left edge of the drawn cat inside the 192-wide cell. The art is not
    /// flush with the cell, so lining the cell up with something on screen
    /// leaves a visible gap; line this up instead.
    /// </summary>
    public const double BodyLeftX = 26;

    private const double TapFrameMs = 120;

    /// <summary>Cell x the paw pad lands on in the "swat" clip.</summary>
    public const double TapPawX = 109;

    /// <summary>Frame the paw makes contact on, counting from zero.</summary>
    public const int TapContactFrame = 4;

    /// <summary>How long into the swat the paw connects.</summary>
    public const double TapContactMs = TapContactFrame * TapFrameMs;

    /// <summary>Cell coordinates of the pinch in the "held" art.</summary>
    public const double GripX = 96;

    public const double GripY = 40;

    private static readonly Dictionary<string, Clip> ByName =
        All.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Names => ByName.Keys;

    public static Clip Get(string name) =>
        ByName.TryGetValue(name, out var clip) ? clip : ByName["idle"];

    public static bool TryGet(string? name, out Clip clip)
    {
        if (name is not null && ByName.TryGetValue(name, out var found))
        {
            clip = found;
            return true;
        }

        clip = ByName["idle"];
        return false;
    }
}
