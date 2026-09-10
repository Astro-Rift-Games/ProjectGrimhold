using System;
using UnityEngine;

/// <summary>
/// A single line of dialogue: speaker metadata, text content, and advance behavior.
/// Serializable so it can be edited directly inside a DialogueSequence asset.
/// </summary>
[Serializable]
public struct DialogueLine
{
    [Tooltip("Display name shown in the UI for the character speaking this line.")]
    public string SpeakerName;

    [Tooltip("Full text content of this line. Revealed character by character during playback.")]
    [TextArea(2, 6)]
    public string Text;

    [Tooltip("Optional portrait sprite for the speaking character. Leave empty for no portrait.")]
    public Sprite SpeakerPortrait;

    [Tooltip("When true, this line advances automatically without requiring player input.")]
    public bool AutoAdvance;

    [Tooltip("Seconds to wait after typing completes before automatically advancing. " +
             "Only applied when AutoAdvance is true.")]
    [Min(0f)]
    public float AutoAdvanceDelay;
}
