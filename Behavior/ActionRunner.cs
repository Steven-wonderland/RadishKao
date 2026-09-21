using System.Diagnostics;
using CatPet.Config;

namespace CatPet.Behavior;

/// <summary>Picks and carries out the configured reaction for a trigger.</summary>
public sealed class ActionRunner
{
    private readonly Random _random = new();

    /// <summary>Weighted pick from the actions listed for a trigger.</summary>
    public TriggerAction? Pick(IReadOnlyList<TriggerAction> actions)
    {
        if (actions.Count == 0)
        {
            return null;
        }

        if (actions.Count == 1)
        {
            return actions[0];
        }

        var total = actions.Sum(a => Math.Max(0, a.Weight));
        if (total <= 0)
        {
            return actions[_random.Next(actions.Count)];
        }

        var roll = _random.NextDouble() * total;
        foreach (var action in actions)
        {
            roll -= Math.Max(0, action.Weight);
            if (roll <= 0)
            {
                return action;
            }
        }

        return actions[^1];
    }

    /// <summary>
    /// Starts whatever the action points at. Returns an error message if the
    /// launch failed, so the caller can show it in the speech bubble.
    /// </summary>
    public string? Launch(TriggerAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Run))
        {
            return null;
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = Environment.ExpandEnvironmentVariables(action.Run),
                UseShellExecute = action.UseShellExecute,
            };

            if (!string.IsNullOrWhiteSpace(action.Args))
            {
                info.Arguments = Environment.ExpandEnvironmentVariables(action.Args);
            }

            if (!string.IsNullOrWhiteSpace(action.WorkingDirectory))
            {
                info.WorkingDirectory = Environment.ExpandEnvironmentVariables(action.WorkingDirectory);
            }

            Process.Start(info);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
