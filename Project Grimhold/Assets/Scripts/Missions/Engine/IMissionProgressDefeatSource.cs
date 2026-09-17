/// <summary>
/// Entity capability exposing the configured mission progress information for its defeat.
/// </summary>
public interface IMissionProgressDefeatSource : IEntity
{
    string TargetId { get; }
    string ZoneId { get; }
    int DefeatProgressAmount { get; }
}
