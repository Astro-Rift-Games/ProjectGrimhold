using System;

/// <summary>Local one-shot playback state; never writes authoritative chest state.</summary>
public sealed class ChestOpeningPresentationState
{
    private bool _initialized;
    private bool _opened;
    private float _elapsed;

    public bool IsOpening { get; private set; }

    public void Initialize(bool opened)
    {
        _initialized = true;
        _opened = opened;
        _elapsed = 0f;
        IsOpening = false;
    }

    public bool Observe(bool opened)
    {
        if (!_initialized)
        {
            Initialize(opened);
            return false;
        }

        if (!opened || _opened)
        {
            return false;
        }

        _opened = true;
        _elapsed = 0f;
        IsOpening = true;
        return true;
    }

    public int Advance(float deltaTime, int frameCount, float framesPerSecond)
    {
        if (!IsOpening)
        {
            return _opened ? frameCount - 1 : 0;
        }

        _elapsed += Math.Max(0f, deltaTime);
        int frame = (int)(_elapsed * framesPerSecond);
        if (frame >= frameCount)
        {
            IsOpening = false;
        }

        return Math.Min(frame, frameCount - 1);
    }

    public void Complete()
    {
        if (_opened)
        {
            IsOpening = false;
        }
    }
}
