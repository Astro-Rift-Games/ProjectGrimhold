/// <summary>
/// Exposes the dialogue sequences configured on a world entity.
/// Implemented alongside IInteractable; intentionally does not extend it
/// to keep interaction and dialogue capabilities segregated (ISP).
/// </summary>
/// <remarks>
/// Primary vs. secondary sequence selection is the responsibility of the
/// local DialoguePresenter, which tracks per-player interaction history.
/// This interface remains stateless and data-only.
/// </remarks>
public interface IDialogueTrigger
{
    /// <summary>
    /// Sequence shown on the first interaction with this entity.
    /// </summary>
    DialogueSequence PrimarySequence { get; }

    /// <summary>
    /// Sequence shown on subsequent interactions.
    /// May be null; the presenter falls back to PrimarySequence when null.
    /// </summary>
    DialogueSequence SecondarySequence { get; }
}
