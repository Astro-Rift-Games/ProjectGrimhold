using UnityEngine;

/// <summary>
/// Configuration asset defining the phase-by-phase visual and effect behavior of light props (torches, braziers, etc.).
/// Multiple assets can be created and assigned to individual props (e.g. NormalTorch, GiantBrazier, FaintCandle).
/// </summary>
[CreateAssetMenu(fileName = "DungeonLightPropConfig", menuName = "Grimhold/Dungeon Pressure/Light Prop Config")]
public sealed class DungeonLightPropConfig : ScriptableObject
{
    [SerializeField] private DungeonLightPropPhaseSettings _normal = new(new Color(1f, 0.65f, 0.35f, 1f), 1.2f, LightEffectPattern.TorchFlicker, 1f);
    [SerializeField] private DungeonLightPropPhaseSettings _reinforcements = new(new Color(1f, 0.6f, 0.25f, 1f), 1.1f, LightEffectPattern.TorchFlicker, 2f);
    [SerializeField] private DungeonLightPropPhaseSettings _criticalPressure = new(new Color(1f, 0.45f, 0.2f, 1f), 0.95f, LightEffectPattern.UnstableFlicker, 2f);
    [SerializeField] private DungeonLightPropPhaseSettings _collapse = new(new Color(1f, 0.25f, 0.15f, 1f), 0.75f, LightEffectPattern.UnstableFlicker, 1.5f);

    public DungeonLightPropPhaseSettings Normal => _normal;
    public DungeonLightPropPhaseSettings Reinforcements => _reinforcements;
    public DungeonLightPropPhaseSettings CriticalPressure => _criticalPressure;
    public DungeonLightPropPhaseSettings Collapse => _collapse;

    public DungeonLightPropPhaseSettings GetSettings(DungeonPressurePhase phase) => phase switch
    {
        DungeonPressurePhase.Normal => _normal,
        DungeonPressurePhase.Reinforcements => _reinforcements,
        DungeonPressurePhase.CriticalPressure => _criticalPressure,
        DungeonPressurePhase.Collapse => _collapse,
        _ => _normal
    };

    public void Initialize(
        DungeonLightPropPhaseSettings normal,
        DungeonLightPropPhaseSettings reinforcements,
        DungeonLightPropPhaseSettings criticalPressure,
        DungeonLightPropPhaseSettings collapse)
    {
        _normal = normal;
        _reinforcements = reinforcements;
        _criticalPressure = criticalPressure;
        _collapse = collapse;
    }
}
