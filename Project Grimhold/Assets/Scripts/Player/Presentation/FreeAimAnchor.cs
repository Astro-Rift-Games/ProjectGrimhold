using UnityEngine;

/// <summary>
/// Places the pivot of a free-aim weapon around the body. The stance offset is expressed in the aim frame: x is
/// the orbit radius along the aim and y is a lateral offset, mirrored with the aim so the weapon stays on the same
/// side of the body when it flips to a left aim. Both axes are relative to the body origin.
/// </summary>
internal static class FreeAimAnchor
{
    internal static Vector2 Resolve(Vector2 aim, Vector2 stanceOffset)
    {
        Vector2 direction = aim.normalized;
        float lateral = direction.x < 0f ? -stanceOffset.y : stanceOffset.y;
        return direction * stanceOffset.x + new Vector2(-direction.y, direction.x) * lateral;
    }
}
