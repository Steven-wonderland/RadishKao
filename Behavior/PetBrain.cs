using CatPet.Config;

namespace CatPet.Behavior;

/// <summary>
/// Decides what the cat does next. Behaviours are picked at random with
/// weights; each one expands into a short queue of <see cref="BehaviorStep"/>.
/// Add an entry to <see cref="Catalog"/> to teach the cat a new idle habit.
///
/// Weights come from config.json when present, so the right-click menu can
/// retune them live; the numbers here are only the starting point.
///
/// The "play" clip is deliberately absent: the ball only comes out when one is
/// actually dropped with a lure hotkey, never off the cat's own bat.
/// </summary>
public sealed class PetBrain(PetConfig config)
{
    public const string AnnoyingModeStep = "annoying_mode";

    private readonly Random _random = new();
    private string _lastBehavior = string.Empty;

    private delegate void Builder(Random rng, PetConfig cfg, List<BehaviorStep> into);

    /// <param name="Key">Name used in config.json.</param>
    /// <param name="Label">What the menu calls it.</param>
    /// <param name="Default">Weight when config says nothing.</param>
    public sealed record Habit(string Key, string Label, double Default);

    private static readonly (Habit Habit, Builder Build)[] Table =
    [
        (new("stand", "發呆", 16), static (rng, _, into) =>
            into.Add(new BehaviorStep("idle", rng.Next(1500, 4200)))),

        (new("stroll", "散步", 26), static (rng, cfg, into) =>
        {
            var dir = rng.Next(2) == 0 ? -1 : 1;
            into.Add(new BehaviorStep("idle", rng.Next(2200, 6500), cfg.WalkSpeed, dir, "walk"));
        }),

        (new("dash", "衝刺", 9), static (rng, cfg, into) =>
        {
            var dir = rng.Next(2) == 0 ? -1 : 1;
            into.Add(new BehaviorStep("idle", rng.Next(900, 2100), cfg.RunSpeed, dir, "run"));
            into.Add(new BehaviorStep("idle", rng.Next(600, 1400)));
        }),

        (new("sit", "坐著", 13), static (rng, _, into) =>
            into.Add(new BehaviorStep("sit", rng.Next(3000, 8000)))),

        (new("look", "張望", 10), static (rng, _, into) =>
        {
            into.Add(new BehaviorStep(rng.Next(2) == 0 ? "look_left" : "look_right", 0));
            into.Add(new BehaviorStep("idle", rng.Next(500, 1300)));
        }),

        (new("nap", "坐著打盹", 6), static (rng, _, into) =>
        {
            into.Add(new BehaviorStep("sleep_in", 0));
            into.Add(new BehaviorStep("sleep_deep", rng.Next(14000, 30000)));
            into.Add(new BehaviorStep("sleep_out", 0));
        }),

        // Flat out on the taskbar. Clicking mid-doze interrupts with the
        // stretch-and-yawn; see PetWindow.Trigger.
        (new("doze", "趴著睡", 14), static (rng, _, into) =>
        {
            into.Add(new BehaviorStep("lie_down", 0));
            into.Add(new BehaviorStep("doze", rng.Next(18000, 40000)));
            into.Add(new BehaviorStep("wake_stretch", 0));
        }),

        (new("annoying", "煩人Mode", 3), static (_, _, into) =>
            into.Add(new BehaviorStep(AnnoyingModeStep, 0))),
    ];

    /// <summary>The tunable habits, for building the menu and the config block.</summary>
    public static IReadOnlyList<Habit> Catalog => [.. Table.Select(entry => entry.Habit)];

    public static double DefaultWeight(string key) =>
        Table.FirstOrDefault(e => e.Habit.Key == key).Habit?.Default ?? 0;

    private double WeightOf(Habit habit) =>
        config.Behaviors.TryGetValue(habit.Key, out var weight)
            ? Math.Max(0, weight)
            : habit.Default;

    /// <summary>Picks the next behaviour, avoiding an immediate repeat.</summary>
    public List<BehaviorStep> Next()
    {
        var steps = new List<BehaviorStep>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var picked = Pick();
            if (picked is null)
            {
                break;
            }

            var (habit, build) = picked.Value;

            if (habit.Key == _lastBehavior && attempt < 5)
            {
                continue;
            }

            _lastBehavior = habit.Key;
            build(_random, config, steps);
            break;
        }

        if (steps.Count == 0)
        {
            steps.Add(new BehaviorStep("idle", 2000));
        }

        return steps;
    }

    private (Habit Habit, Builder Build)? Pick()
    {
        var total = Table.Sum(entry => WeightOf(entry.Habit));
        if (total <= 0)
        {
            return null;
        }

        var roll = _random.NextDouble() * total;

        foreach (var entry in Table)
        {
            roll -= WeightOf(entry.Habit);
            if (roll <= 0)
            {
                return entry;
            }
        }

        return Table[0];
    }
}
