using System;

/// <summary>
/// Controls the progression of an active dialogue sequence.
/// Responsible for line indexing, character-by-character typing timing,
/// auto-advance, and manual advance. Does not touch UI or network state.
/// </summary>
public interface IDialogueController
{
    /// <summary>True while a dialogue sequence is actively running.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Raised when a new line begins typing.
    /// Parameters: line data, current line index (0-based), total line count.
    /// </summary>
    event Action<DialogueLine, int, int> LineStarted;

    /// <summary>
    /// Raised each time a character is appended to the typed output.
    /// Parameter: the partial text built so far.
    /// Consumers may use this to update a UI text field incrementally.
    /// </summary>
    event Action<string> CharacterTyped;

    /// <summary>
    /// Raised when the last line in the sequence finishes and the dialogue closes.
    /// </summary>
    event Action DialogueEnded;

    /// <summary>
    /// Starts playing the given sequence from the first line.
    /// No-op if a sequence is already active.
    /// </summary>
    void StartDialogue(DialogueSequence sequence);

    /// <summary>
    /// If typing is still in progress: skips to the end of the current line instantly.
    /// If typing has completed: advances to the next line, or ends the dialogue
    /// when the current line is the last one.
    /// No-op when no dialogue is active.
    /// </summary>
    void Advance();

    /// <summary>
    /// Immediately ends the active dialogue regardless of current state.
    /// DialogueEnded is raised. No-op when no dialogue is active.
    /// </summary>
    void ForceEnd();
}
