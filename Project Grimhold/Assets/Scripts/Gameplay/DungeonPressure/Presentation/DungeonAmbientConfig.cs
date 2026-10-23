using UnityEngine;

/// <summary>
/// Configuration asset defining the phase-by-phase global ambient lighting, camera shake, and post-process vignette behavior for the dungeon.
/// Supports entry shake, continuous sustained tremor, periodic tremors, and native URP Volume vignette styling.
/// </summary>
[CreateAssetMenu(fileName = "DungeonAmbientConfig", menuName = "Grimhold/Dungeon Pressure/Ambient Config")]
public sealed class DungeonAmbientConfig : ScriptableObject
{
    [SerializeField] private DungeonAmbientPhaseSettings _normal = new(
        new Color(0.85f, 0.88f, 0.92f, 1f), 0.85f, 1f,
        0f, 0f, 0f,
        0f, 0f, 0f,
        Color.black, 0f, 0.4f, false, 1f);

    [SerializeField] private DungeonAmbientPhaseSettings _reinforcements = new(
        new Color(0.78f, 0.74f, 0.63f, 1f), 0.75f, 2f,
        0.08f, 0.6f, 0f,
        0f, 0f, 0f,
        Color.black, 0f, 0.4f, false, 2f);

    [SerializeField] private DungeonAmbientPhaseSettings _criticalPressure = new(
        new Color(0.65f, 0.40f, 0.28f, 1f), 0.65f, 2f,
        0.25f, 1.2f, 0f,
        0.15f, 0.8f, 25f,
        new Color(0.12f, 0.04f, 0.02f, 1f), 0.32f, 0.45f, false, 2f);

    [SerializeField] private DungeonAmbientPhaseSettings _collapse = new(
        new Color(0.49f, 0.15f, 0.15f, 1f), 0.55f, 1.5f,
        0.75f, 2.5f, 0.08f,
        0.35f, 1.2f, 12f,
        new Color(0.35f, 0.03f, 0.03f, 1f), 0.65f, 0.55f, false, 1.5f);

    public DungeonAmbientPhaseSettings Normal => _normal;
    public DungeonAmbientPhaseSettings Reinforcements => _reinforcements;
    public DungeonAmbientPhaseSettings CriticalPressure => _criticalPressure;
    public DungeonAmbientPhaseSettings Collapse => _collapse;

    public DungeonAmbientPhaseSettings GetSettings(DungeonPressurePhase phase) => phase switch
    {
        DungeonPressurePhase.Normal => _normal,
        DungeonPressurePhase.Reinforcements => _reinforcements,
        DungeonPressurePhase.CriticalPressure => _criticalPressure,
        DungeonPressurePhase.Collapse => _collapse,
        _ => _normal
    };

    public void Initialize(
        DungeonAmbientPhaseSettings normal,
        DungeonAmbientPhaseSettings reinforcements,
        DungeonAmbientPhaseSettings criticalPressure,
        DungeonAmbientPhaseSettings collapse)
    {
        _normal = normal;
        _reinforcements = reinforcements;
        _criticalPressure = criticalPressure;
        _collapse = collapse;
    }
}
