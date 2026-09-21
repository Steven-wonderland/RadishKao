namespace CatPet.Behavior;

/// <summary>
/// One beat of a behaviour: hold a clip for a while, optionally moving.
/// </summary>
/// <param name="Clip">Clip name used when <paramref name="Direction"/> is 0.</param>
/// <param name="DurationMs">How long to stay on this step; 0 or less means "until the clip finishes once".</param>
/// <param name="Speed">Movement speed in device-independent pixels per second (unsigned).</param>
/// <param name="Direction">-1 walks left, +1 walks right, 0 stands still.</param>
/// <param name="DirectionalClip">
/// Clip prefix for moving steps; "walk" resolves to walk_left / walk_right so the
/// cat can turn around at the edge of the screen without changing behaviour.
/// </param>
public sealed record BehaviorStep(
    string Clip,
    double DurationMs,
    double Speed = 0,
    int Direction = 0,
    string? DirectionalClip = null)
{
    public bool RunsUntilClipEnds => DurationMs <= 0;

    /// <summary>Clip to show for the current facing.</summary>
    public string ResolveClip(int direction)
    {
        if (DirectionalClip is null || direction == 0)
        {
            return Clip;
        }

        return direction < 0 ? $"{DirectionalClip}_left" : $"{DirectionalClip}_right";
    }
}
