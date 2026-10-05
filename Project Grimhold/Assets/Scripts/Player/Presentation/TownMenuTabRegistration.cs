using System;
using UnityEngine;

/// <summary>
/// Describes one tab a feature plugs into the Town player menu. The menu reparents
/// <see cref="Content"/> into its window and calls <see cref="Shown"/>/<see cref="Hidden"/>;
/// the feature keeps ownership of its own visibility and state.
/// </summary>
public sealed class TownMenuTabRegistration
{
    public TownMenuTabRegistration(
        string id,
        string label,
        RectTransform content,
        Action shown,
        Action hidden)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A tab id is required.", nameof(id));
        }

        Id = id;
        Label = string.IsNullOrWhiteSpace(label) ? id : label;
        Content = content;
        Shown = shown;
        Hidden = hidden;
    }

    public string Id { get; }
    public string Label { get; }
    public RectTransform Content { get; }
    public Action Shown { get; }
    public Action Hidden { get; }
}
