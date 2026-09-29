using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueCompletionPlayModeTests
{
    [UnityTest]
    public IEnumerator AdvancingPastLastLine_CompletesNormallyExactlyOnce()
    {
        var gameObject = new GameObject(nameof(DialogueCompletionPlayModeTests));
        var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
        sequence.Lines = new[] { new DialogueLine { Text = string.Empty } };
        var controller = gameObject.AddComponent<DialogueController>();
        int completed = 0;
        int ended = 0;
        controller.DialogueCompletedNormally += () => completed++;
        controller.DialogueEnded += () => ended++;

        controller.StartDialogue(sequence);
        yield return null;
        controller.Advance();
        controller.Advance();

        Assert.That(controller.IsActive, Is.False);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(ended, Is.EqualTo(1));
        Object.Destroy(gameObject);
        Object.Destroy(sequence);
    }

    [UnityTest]
    public IEnumerator ForceEndAndDisable_NeverCompleteNormally()
    {
        var gameObject = new GameObject(nameof(DialogueCompletionPlayModeTests));
        var sequence = ScriptableObject.CreateInstance<DialogueSequence>();
        sequence.Lines = new[] { new DialogueLine { Text = "Still typing" } };
        var controller = gameObject.AddComponent<DialogueController>();
        int completed = 0;
        int ended = 0;
        controller.DialogueCompletedNormally += () => completed++;
        controller.DialogueEnded += () => ended++;

        controller.StartDialogue(sequence);
        controller.ForceEnd();
        controller.StartDialogue(sequence);
        gameObject.SetActive(false);
        yield return null;

        Assert.That(controller.IsActive, Is.False);
        Assert.That(completed, Is.Zero);
        Assert.That(ended, Is.EqualTo(2));
        Object.Destroy(gameObject);
        Object.Destroy(sequence);
    }
}
