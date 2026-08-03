/// <summary>
/// Entity capability exposing the configured extraction-progress reward for its defeat.
/// </summary>
public interface IExtractionProgressDefeatSource : IEntity
{
    int DefeatProgressReward { get; }
}
