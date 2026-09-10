using UnityEngine;

/// <summary>
/// ScriptableObject that defines a complete dialogue sequence for one interaction.
/// Assign as PrimarySequence or SecondarySequence on a DialogueInteractable component.
/// </summary>
/// <remarks>
/// Typing speed and sound are sequence-level settings, so different NPCs or
/// interaction states can have distinct feel without code changes.
/// </remarks>
[CreateAssetMenu(
    fileName = "NewDialogueSequence",
    menuName = "Grimhold/Dialogue/Sequence")]
public sealed class DialogueSequence : ScriptableObject
{
    [Header("Typing Speed")]
    [Tooltip("Number of characters revealed per second during the typing effect. " +
             "Higher values produce faster typing.")]
    [Min(1f)]
    public float CharactersPerSecond = 40f;

    [Header("Typing Sound")]
    [Tooltip("Optional audio clip played each time a character is typed. " +
             "Uses AudioManager.Instance.PlaySfx at the player's position. " +
             "Supports randomized clips via CustomClip's GetRandomClip().")]
    public CustomClip TypingSound;

    [Header("Lines")]
    [Tooltip("Ordered list of lines that compose this sequence.")]
    public DialogueLine[] Lines;
}
