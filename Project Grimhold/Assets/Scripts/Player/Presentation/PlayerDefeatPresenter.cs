using UnityEngine;

/// <summary>
/// Presenter component responsible for procedural player defeat animation and visual pose.
/// Inherits core defeat presentation logic from <see cref="DefeatPresenterBase"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDefeatPresenter : DefeatPresenterBase
{
    protected override bool HideBodyVisualAfterTransition => false;
}
