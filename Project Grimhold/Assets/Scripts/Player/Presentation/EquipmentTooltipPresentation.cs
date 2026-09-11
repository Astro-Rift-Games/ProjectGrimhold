/// <summary>Result of resolving one catalog definition for inventory tooltip presentation.</summary>
public enum EquipmentTooltipPresentationStatus : byte
{
    UnresolvedDefinition = 0,
    FunctionalStatistics = 1,
    NoEquipmentStatistics = 2,
    InvalidEquipmentConfiguration = 3
}

/// <summary>
/// Immutable, disposable presentation value for an inventory equipment tooltip.
/// It contains only definition-owned values and never carries gameplay state.
/// </summary>
public readonly struct EquipmentTooltipPresentation
{
    public EquipmentTooltipPresentationStatus Status { get; }
    public string Title { get; }
    public string Body { get; }
    public bool CanShow => Status != EquipmentTooltipPresentationStatus.UnresolvedDefinition;

    public EquipmentTooltipPresentation(
        EquipmentTooltipPresentationStatus status,
        string title,
        string body)
    {
        Status = status;
        Title = title ?? string.Empty;
        Body = body ?? string.Empty;
    }
}
