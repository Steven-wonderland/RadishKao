namespace CatPet.Sprites;

/// <summary>Holds the playhead for whichever clip is currently on screen.</summary>
public sealed class Animator
{
    private double _elapsedMs;
    private int _step;

    public Animator(Clip clip) => Current = clip;

    public Clip Current { get; private set; }

    /// <summary>How many times the clip has reached its last frame since it started.</summary>
    public int CompletedCycles { get; private set; }

    public int Row => Current.Row;

    public int Column => Current.ColumnForStep(_step);

    public void Play(Clip clip, bool restartIfSame = false)
    {
        if (ReferenceEquals(clip, Current) && !restartIfSame)
        {
            return;
        }

        Current = clip;
        _elapsedMs = 0;
        _step = 0;
        CompletedCycles = 0;
    }

    public void Advance(double deltaMs)
    {
        if (Current.FrameMs <= 0)
        {
            return;
        }

        _elapsedMs += deltaMs;
        var steps = Current.StepCount;

        while (_elapsedMs >= Current.FrameMs)
        {
            _elapsedMs -= Current.FrameMs;

            if (_step + 1 >= steps)
            {
                CompletedCycles++;

                if (Current.Loop)
                {
                    _step = 0;
                }
                else
                {
                    _step = steps - 1;
                    _elapsedMs = 0;
                    break;
                }
            }
            else
            {
                _step++;
            }
        }
    }
}
