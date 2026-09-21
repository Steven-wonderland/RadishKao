using CatPet.Config;
using CatPet.Windows;

namespace CatPet.Behavior;

public enum ChasePhase
{
    /// <summary>Running over to the thing that was just dropped.</summary>
    Outbound,

    /// <summary>Playing with it / eating it on the spot.</summary>
    Enjoy,

    /// <summary>Heading back down to the taskbar.</summary>
    Home,
}

/// <summary>State for one round of "something was dropped, go and get it".</summary>
internal sealed class Chase(LureConfig lure, PropWindow prop, double propX, double propY)
{
    public LureConfig Lure { get; } = lure;

    public PropWindow Prop { get; } = prop;

    /// <summary>Where the item sits, in physical screen pixels.</summary>
    public double PropX { get; } = propX;

    public double PropY { get; } = propY;

    public ChasePhase Phase { get; set; } = ChasePhase.Outbound;

    public double PhaseElapsedMs { get; set; }

    /// <summary>Destination for the cat's sprite-left x, physical pixels.</summary>
    public double TargetX { get; set; }

    /// <summary>Destination for the cat's paw line, physical pixels.</summary>
    public double TargetY { get; set; }
}
