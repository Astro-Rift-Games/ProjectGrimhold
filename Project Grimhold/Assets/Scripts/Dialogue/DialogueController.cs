using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Implements IDialogueController. Processes a DialogueSequence line by line,
/// handling typing effects, auto-advance, and audio playback via AudioManager.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueController : MonoBehaviour, IDialogueController
{
    public bool IsActive { get; private set; }

    public event Action<DialogueLine, int, int> LineStarted;
    public event Action<string> CharacterTyped;
    public event Action DialogueEnded;

    private DialogueSequence _currentSequence;
    private int _currentLineIndex;
    
    private Coroutine _typingRoutine;
    private Coroutine _autoAdvanceRoutine;
    private bool _isTyping;

    public void StartDialogue(DialogueSequence sequence)
    {
        if (IsActive) return;
        if (sequence == null || sequence.Lines == null || sequence.Lines.Length == 0) return;

        _currentSequence = sequence;
        _currentLineIndex = 0;
        IsActive = true;

        PlayCurrentLine();
    }

    public void Advance()
    {
        if (!IsActive) return;

        if (_isTyping)
        {
            CompleteTypingInstantly();
        }
        else
        {
            _currentLineIndex++;
            if (_currentLineIndex >= _currentSequence.Lines.Length)
            {
                ForceEnd();
            }
            else
            {
                PlayCurrentLine();
            }
        }
    }

    public void ForceEnd()
    {
        if (!IsActive) return;

        StopAllRoutines();
        
        IsActive = false;
        _currentSequence = null;
        _currentLineIndex = 0;
        
        DialogueEnded?.Invoke();
    }

    private void PlayCurrentLine()
    {
        StopAllRoutines();
        
        DialogueLine line = _currentSequence.Lines[_currentLineIndex];
        LineStarted?.Invoke(line, _currentLineIndex, _currentSequence.Lines.Length);
        
        _typingRoutine = StartCoroutine(TypeLineRoutine(line));
    }

    private IEnumerator TypeLineRoutine(DialogueLine line)
    {
        _isTyping = true;
        string fullText = line.Text ?? string.Empty;
        
        float timePerCharacter = 1f / Mathf.Max(1f, _currentSequence.CharactersPerSecond);
        WaitForSeconds wait = new WaitForSeconds(timePerCharacter);

        for (int i = 1; i <= fullText.Length; i++)
        {
            string partialText = fullText.Substring(0, i);
            CharacterTyped?.Invoke(partialText);

            if (_currentSequence.TypingSound.IsValid && AudioManager.Instance != null)
            {
                // UI sounds are typically 2D, so Vector3.zero is fine here.
                // The CustomClip SpatialBlend determines actual spatialization.
                AudioManager.Instance.PlaySfx(_currentSequence.TypingSound, Vector3.zero);
            }

            yield return wait;
        }

        OnTypingCompleted(line);
    }

    private void CompleteTypingInstantly()
    {
        StopAllRoutines();
        
        DialogueLine line = _currentSequence.Lines[_currentLineIndex];
        string fullText = line.Text ?? string.Empty;
        
        CharacterTyped?.Invoke(fullText);
        OnTypingCompleted(line);
    }

    private void OnTypingCompleted(DialogueLine line)
    {
        _isTyping = false;

        if (line.AutoAdvance)
        {
            _autoAdvanceRoutine = StartCoroutine(AutoAdvanceRoutine(line.AutoAdvanceDelay));
        }
    }

    private IEnumerator AutoAdvanceRoutine(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        Advance();
    }

    private void StopAllRoutines()
    {
        if (_typingRoutine != null)
        {
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }
        if (_autoAdvanceRoutine != null)
        {
            StopCoroutine(_autoAdvanceRoutine);
            _autoAdvanceRoutine = null;
        }
        _isTyping = false;
    }

    private void OnDisable()
    {
        ForceEnd();
    }
}
