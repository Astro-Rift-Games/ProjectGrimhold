using UnityEngine;

/// <summary>
/// Abstracts the UI panel responsible for presenting dialogue to the player.
/// Implementations must not contain business logic; they only reflect state
/// pushed by the DialoguePresenter.
/// </summary>
public interface IDialogueView
{
    /// <summary>
    /// Makes the dialogue panel visible and populates speaker metadata.
    /// Called once at the start of each line.
    /// </summary>
    /// <param name="speakerName">Display name of the character speaking.</param>
    /// <param name="portrait">Optional portrait sprite; null is a valid value.</param>
    void Show(string speakerName, Sprite portrait);

    /// <summary>
    /// Updates the displayed text with the partially typed string.
    /// Called repeatedly during the typing coroutine.
    /// </summary>
    /// <param name="partialText">Text built so far, always a prefix of the full line.</param>
    void UpdateTypedText(string partialText);

    /// <summary>
    /// Snaps the displayed text to the full line content immediately.
    /// Called when the player skips typing or auto-advance fires.
    /// </summary>
    /// <param name="fullText">Complete text of the current line.</param>
    void CompleteText(string fullText);

    /// <summary>
    /// Hides the dialogue panel. Called when dialogue ends or is force-ended.
    /// </summary>
    void Hide();
}
