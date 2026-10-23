using System.Collections.Generic;

/// <summary>
/// Central registry for active <see cref="DungeonLightProp"/> components in the scene.
/// Allows <see cref="DungeonPressureAmbientPresenter"/> to notify props on phase transitions
/// without expensive hierarchy queries or tight coupling.
/// </summary>
public static class DungeonLightPropRegistry
{
    private static readonly List<DungeonLightProp> s_Props = new();

    public static DungeonPressurePhase CurrentPhase { get; private set; } = DungeonPressurePhase.Normal;

    public static int Count
    {
        get
        {
            // Prune destroyed Unity objects
            for (int i = s_Props.Count - 1; i >= 0; i--)
            {
                if (s_Props[i] == null)
                {
                    s_Props.RemoveAt(i);
                }
            }
            return s_Props.Count;
        }
    }

    public static void Register(DungeonLightProp prop)
    {
        if (ReferenceEquals(prop, null) || s_Props.Contains(prop))
        {
            return;
        }

        s_Props.Add(prop);

        // If the dungeon has already progressed past Normal, immediately synchronize this prop.
        if (CurrentPhase != DungeonPressurePhase.Normal)
        {
            prop.ApplyPhase(CurrentPhase, immediate: true);
        }
    }

    public static void Unregister(DungeonLightProp prop)
    {
        if (!ReferenceEquals(prop, null))
        {
            s_Props.Remove(prop);
        }
    }

    public static void NotifyPhaseChanged(DungeonPressurePhase phase, bool immediate)
    {
        CurrentPhase = phase;

        for (int i = s_Props.Count - 1; i >= 0; i--)
        {
            DungeonLightProp prop = s_Props[i];
            if (prop == null)
            {
                s_Props.RemoveAt(i);
                continue;
            }

            prop.ApplyPhase(phase, immediate);
        }
    }

    public static void Clear()
    {
        s_Props.Clear();
        CurrentPhase = DungeonPressurePhase.Normal;
    }
}
