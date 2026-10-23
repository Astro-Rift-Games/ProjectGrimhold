/// <summary>
/// Defines the illumination effect pattern applied to dungeon light props (e.g. torches, braziers).
/// </summary>
public enum LightEffectPattern : byte
{
    /// <summary>Constant light without temporal variation.</summary>
    Steady = 0,

    /// <summary>Organic, gentle flame flicker (Perlin noise).</summary>
    TorchFlicker = 1,

    /// <summary>Erratic, agitated flicker with rapid micro-drops (ominous/collapse).</summary>
    UnstableFlicker = 2,

    /// <summary>Slow rhythmic sinusoidal breathing/pulsing.</summary>
    Pulse = 3
}
