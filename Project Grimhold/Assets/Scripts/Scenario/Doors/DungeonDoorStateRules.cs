/// <summary>
/// Pure door lifecycle policy shared by State Authority simulation and local presentation.
/// Interaction is accepted only in stable states; a door cannot close onto an occupied doorway.
/// </summary>
public static class DungeonDoorStateRules
{
    public const string OpenPromptText = "Abrir puerta";
    public const string ClosePromptText = "Cerrar puerta";

    /// <summary>Opens away from the interactor; equal pivot Y chooses North.</summary>
    public static DungeonDoorOpenDirection ResolveOpenDirection(float interactorY, float pivotY)
    {
        return interactorY > pivotY ? DungeonDoorOpenDirection.South : DungeonDoorOpenDirection.North;
    }

    public static bool IsStable(DungeonDoorState state)
    {
        return state == DungeonDoorState.Closed || state == DungeonDoorState.Open;
    }

    /// <summary>
    /// Starts the transition requested by an interaction. Transitions and closing onto an
    /// occupied doorway are rejected and leave <paramref name="next"/> equal to the current state.
    /// </summary>
    public static bool TryBeginToggle(DungeonDoorState current, bool isDoorwayOccupied, out DungeonDoorState next)
    {
        next = current;
        switch (current)
        {
            case DungeonDoorState.Closed:
                next = DungeonDoorState.Opening;
                return true;
            case DungeonDoorState.Open when !isDoorwayOccupied:
                next = DungeonDoorState.Closing;
                return true;
            default:
                return false;
        }
    }

    public static DungeonDoorState CompleteTransition(DungeonDoorState current)
    {
        return current switch
        {
            DungeonDoorState.Opening => DungeonDoorState.Open,
            DungeonDoorState.Closing => DungeonDoorState.Closed,
            _ => current
        };
    }

    /// <summary>The doorway stays solid until opening completes and becomes solid as soon as closing starts.</summary>
    public static bool BlocksPassage(DungeonDoorState state)
    {
        return state != DungeonDoorState.Open;
    }

    /// <summary>Returns the contextual action, or null while the door is moving.</summary>
    public static string ResolvePrompt(DungeonDoorState state)
    {
        return state switch
        {
            DungeonDoorState.Closed => OpenPromptText,
            DungeonDoorState.Open => ClosePromptText,
            _ => null
        };
    }
}
