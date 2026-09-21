using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatPet.Config;

public sealed class PetConfig
{
    /// <summary>Sheet path, relative to the exe folder or absolute.</summary>
    public string SpriteSheet { get; set; } = "Assets/cat-spritesheet.png";

    /// <summary>Size of one 192x208 cell on screen, 1.0 = full size.</summary>
    public double Scale { get; set; } = 0.52;

    /// <summary>Strolling speed, device-independent pixels per second.</summary>
    public double WalkSpeed { get; set; } = 48;

    /// <summary>Dash speed, device-independent pixels per second.</summary>
    public double RunSpeed { get; set; } = 130;

    /// <summary>Nudge the cat down into the taskbar (positive) or up off it (negative).</summary>
    public double FootOffset { get; set; }

    /// <summary>How long a speech bubble stays up.</summary>
    public double BubbleSeconds { get; set; } = 2.5;

    /// <summary>Let the cat be dragged around with the left mouse button.</summary>
    public bool Draggable { get; set; } = true;

    /// <summary>Line said on start-up. Empty (the default) for a quiet entrance.</summary>
    public string? Greeting { get; set; }

    /// <summary>
    /// Shrink the cat as it runs up the screen after a lure and grow it back on
    /// the way down, so the chase reads as depth. Sideways runs stay the same
    /// size because the effect is driven purely by height above the taskbar.
    /// </summary>
    public bool DepthEffect { get; set; } = true;

    /// <summary>How small the cat gets at the very top of the screen.</summary>
    public double MinDepth { get; set; } = 0.55;

    /// <summary>
    /// How often each idle habit is chosen, by key from PetBrain.Catalog.
    /// Missing keys fall back to the built-in weight; the right-click menu
    /// writes this block.
    /// </summary>
    public Dictionary<string, double> Behaviors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Clip played when the cat swats the Start button. Left as a sit until the
    /// swat art exists; point it at the new clip when it does.
    /// </summary>
    public string StartSwatAnimation { get; set; } = "sit";

    /// <summary>
    /// How far into the swat the Start menu pops, so the two line up instead of
    /// the menu beating the paw to it.
    /// </summary>
    public double StartSwatDelayMs { get; set; } = 320;

    /// <summary>
    /// Synthesise a real Windows key press instead of asking the shell politely.
    /// Off by default: a stray modifier held at that moment would turn it into
    /// a Win+key shortcut.
    /// </summary>
    public bool StartSwatUsesWinKey { get; set; }

    /// <summary>Hotkey-summoned things the cat will run after.</summary>
    public List<LureConfig> Lures { get; set; } = [];

    /// <summary>
    /// What the cat does when you interact with it. Recognised keys:
    /// leftClick, doubleClick, middleClick, pickUp, drop.
    /// </summary>
    public Dictionary<string, List<TriggerAction>> Triggers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PetConfig Load(string path, out string? error)
    {
        error = null;

        try
        {
            if (!File.Exists(path))
            {
                return new PetConfig();
            }

            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<PetConfig>(json, Options) ?? new PetConfig();

            // Deserialization hands back a plain dictionary, so "LeftClick" and
            // "leftclick" would otherwise be silently ignored.
            config.Triggers = new Dictionary<string, List<TriggerAction>>(
                config.Triggers, StringComparer.OrdinalIgnoreCase);

            config.Behaviors = new Dictionary<string, double>(
                config.Behaviors, StringComparer.OrdinalIgnoreCase);

            return config;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new PetConfig();
        }
    }

    public IReadOnlyList<TriggerAction> ActionsFor(string trigger) =>
        Triggers.TryGetValue(trigger, out var actions) ? actions : [];
}
