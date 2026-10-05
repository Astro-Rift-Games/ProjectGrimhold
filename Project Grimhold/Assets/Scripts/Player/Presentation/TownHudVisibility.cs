using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides and restores a set of Town HUD roots through a <see cref="CanvasGroup"/> so the HUD's
/// own activation logic is never overridden. Original group values are restored on show.
/// </summary>
public sealed class TownHudVisibility
{
    private readonly List<Entry> _entries = new();

    public bool IsHidden { get; private set; }

    /// <summary>Starts controlling the target; applies the current hidden state immediately.</summary>
    public void Register(GameObject target)
    {
        if (target == null || IndexOf(target) >= 0)
        {
            return;
        }

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = target.AddComponent<CanvasGroup>();
        }

        var entry = new Entry(target, group);
        _entries.Add(entry);
        if (IsHidden)
        {
            entry.Hide();
        }
    }

    /// <summary>Stops controlling the target and restores its original group values.</summary>
    public void Unregister(GameObject target)
    {
        int index = target != null ? IndexOf(target) : -1;
        if (index < 0)
        {
            return;
        }

        _entries[index].Restore();
        _entries.RemoveAt(index);
    }

    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            if (entry.Group == null)
            {
                _entries.RemoveAt(i);
                continue;
            }

            if (hidden)
            {
                entry.Hide();
            }
            else
            {
                entry.Restore();
            }
        }
    }

    private int IndexOf(GameObject target)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Target == target)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class Entry
    {
        private readonly float _alpha;
        private readonly bool _interactable;
        private readonly bool _blocksRaycasts;
        private bool _hidden;

        public Entry(GameObject target, CanvasGroup group)
        {
            Target = target;
            Group = group;
            _alpha = group.alpha;
            _interactable = group.interactable;
            _blocksRaycasts = group.blocksRaycasts;
        }

        public GameObject Target { get; }
        public CanvasGroup Group { get; }

        public void Hide()
        {
            if (Group == null || _hidden)
            {
                return;
            }

            _hidden = true;
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }

        public void Restore()
        {
            if (!_hidden)
            {
                return;
            }

            _hidden = false;
            if (Group == null)
            {
                return;
            }

            Group.alpha = _alpha;
            Group.interactable = _interactable;
            Group.blocksRaycasts = _blocksRaycasts;
        }
    }
}
