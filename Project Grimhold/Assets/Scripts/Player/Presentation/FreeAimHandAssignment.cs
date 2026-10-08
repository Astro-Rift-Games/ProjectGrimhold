using UnityEngine;

/// <summary>Hand targets a free-aim weapon asks the presenter to pose, in the visual root space.</summary>
internal readonly struct FreeAimHandTargets
{
    internal FreeAimHandTargets(bool drivesLeftHand, Vector2 leftHand, bool drivesRightHand, Vector2 rightHand)
    {
        DrivesLeftHand = drivesLeftHand;
        LeftHand = leftHand;
        DrivesRightHand = drivesRightHand;
        RightHand = rightHand;
    }

    internal bool DrivesLeftHand { get; }
    internal Vector2 LeftHand { get; }
    internal bool DrivesRightHand { get; }
    internal Vector2 RightHand { get; }
}

/// <summary>
/// Chooses which hand takes which grip under free aim. A weapon-driven rig (bows, crossbow) is held by the left
/// hand on its handle, and the right hand takes the secondary grip (the string or stock). A hand-held rig keeps the
/// right hand on the grip, and a two-handed one adds the left hand on the secondary grip.
/// </summary>
internal static class FreeAimHandAssignment
{
    internal static FreeAimHandTargets Resolve(bool weaponDriven, bool twoHanded, RangedWeaponAimPose pose)
    {
        if (weaponDriven)
        {
            return new FreeAimHandTargets(true, pose.MainHandPosition, true, pose.SecondaryHandPosition);
        }

        return new FreeAimHandTargets(twoHanded, pose.SecondaryHandPosition, true, pose.MainHandPosition);
    }
}
