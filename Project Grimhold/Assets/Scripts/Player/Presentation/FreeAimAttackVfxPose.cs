using UnityEngine;

/// <summary>
/// Builds the attack VFX pose of a free-aim weapon from the live weapon pivot instead of an authored facing
/// bucket. The anchor and rotation follow the pivot, while the authored art lead (reach offset and depth) is
/// kept, so the visual keeps its size and timing.
/// </summary>
internal static class FreeAimAttackVfxPose
{
    /// <summary>Resolve the same free-aim contract at an independently animated holding grip.</summary>
    internal static AttackVfxDefinition.DirectionalPose ResolveAtGrip(
        Transform visualRoot, Transform grip, Vector2 localAim, bool mirrored,
        AttackVfxDefinition.DirectionalPose authoredLead, int sortingOrder)
    {
        Vector3 anchor = visualRoot.InverseTransformPoint(grip.position);
        Vector3 axis = visualRoot.InverseTransformVector(grip.TransformVector(localAim));
        return Resolve(anchor, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg,
            mirrored, authoredLead, sortingOrder);
    }

    /// <summary>
    /// <paramref name="anchor"/> is the pivot position in the Animator root space and
    /// <paramref name="pivotAngleDegrees"/> its angle. <paramref name="authoredLead"/> supplies the reach offset
    /// and depth only.
    /// </summary>
    internal static AttackVfxDefinition.DirectionalPose Resolve(
        Vector2 anchor,
        float pivotAngleDegrees,
        bool mirrored,
        AttackVfxDefinition.DirectionalPose authoredLead,
        int sortingOrder)
    {
        return new AttackVfxDefinition.DirectionalPose(
            new Vector3(anchor.x, anchor.y, authoredLead.Position.z),
            new Vector3(0f, 0f, pivotAngleDegrees),
            authoredLead.ReachOffset,
            mirrored,
            sortingOrder);
    }
}
