using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FreeAimAttackVfxPoseTests
{
    private const float Tolerance = 0.0001f;
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private static readonly Vector2 Anchor = new Vector2(0.3f, 0.45f);

    private static readonly Vector2[] Aims =
    {
        Vector2.right, Vector2.up, Vector2.left, Vector2.down,
        new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.8f), new Vector2(0.6f, -0.8f)
    };

    [Test]
    public void Resolve_AnchorsOnThePivotAndAimsAlongThePivotAngle()
    {
        AttackVfxDefinition.DirectionalPose lead = LongBowVfx().GetPose(3);

        AttackVfxDefinition.DirectionalPose pose = FreeAimAttackVfxPose.Resolve(
            Anchor, 90f, mirrored: false, lead, sortingOrder: 21);

        Assert.That(pose.Position.x, Is.EqualTo(Anchor.x).Within(Tolerance));
        Assert.That(pose.Position.y, Is.EqualTo(Anchor.y).Within(Tolerance));
        Assert.That(pose.Position.z, Is.EqualTo(lead.Position.z).Within(Tolerance));
        Assert.That(Quaternion.Angle(pose.Rotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(0.001f));
        Assert.That(pose.ReachOffset, Is.EqualTo(lead.ReachOffset).Within(Tolerance), "The authored art lead is kept.");
        Assert.That(pose.SortingOrder, Is.EqualTo(21));
        Assert.That(pose.IsValid, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Resolve_CarriesTheMirror(bool mirrored)
    {
        AttackVfxDefinition.DirectionalPose pose = FreeAimAttackVfxPose.Resolve(
            Anchor, 10f, mirrored, LongBowVfx().GetPose(3), 21);

        Assert.That(pose.Mirrored, Is.EqualTo(mirrored));
    }

    [Test]
    public void BowShot_ReleasesFromThePivotAlongEveryAim()
    {
        AttackVfxDefinition vfx = LongBowVfx();
        var visual = AssetDatabase.LoadAssetAtPath<BowShotVfxVisualDefinition>(Definitions + "BowShotVfxVisual.asset");
        float size = vfx.GetPose(3).ReachOffset;

        foreach (Vector2 aim in Aims)
        {
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            AttackVfxDefinition.DirectionalPose pose = FreeAimAttackVfxPose.Resolve(
                Anchor, angle, aim.x < 0f, vfx.GetPose(3), 21);

            Assert.That(vfx.TryResolvePose(pose, 0f, out AttackVfxDefinition.ResolvedPose resolved), Is.True);
            Vector3 shotOrigin = resolved.Position +
                resolved.Rotation * Vector3.Scale((Vector3)visual.Origin, resolved.Scale);
            Vector3 expected = (Vector3)Anchor + (Vector3)(aim.normalized * size);

            Assert.That(shotOrigin.x, Is.EqualTo(expected.x).Within(Tolerance), aim.ToString());
            Assert.That(shotOrigin.y, Is.EqualTo(expected.y).Within(Tolerance), aim.ToString());
        }
    }

    [Test]
    public void CastFlash_LandsOnTheCastPointAlongEveryAim()
    {
        AttackVfxDefinition vfx = AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(
            Definitions + "MagicWandCastFlashAttackVfx.asset");
        var visual = AssetDatabase.LoadAssetAtPath<CastFlashVfxVisualDefinition>(Definitions + "CastFlashVfxVisual.asset");
        float reach = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            Definitions + "MagicWandWeaponDefinition.asset").Presentation.BladeReach;
        float size = vfx.GetPose(3).ReachOffset + reach;

        foreach (Vector2 aim in Aims)
        {
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            AttackVfxDefinition.DirectionalPose pose = FreeAimAttackVfxPose.Resolve(
                Anchor, angle, aim.x < 0f, vfx.GetPose(3), 21);

            Assert.That(vfx.TryResolvePose(pose, reach, out AttackVfxDefinition.ResolvedPose resolved), Is.True);
            Vector3 castPoint = resolved.Position + (Vector3)visual.Center;
            Vector3 expected = (Vector3)Anchor + (Vector3)(aim.normalized * size);

            Assert.That(castPoint.x, Is.EqualTo(expected.x).Within(Tolerance), aim.ToString());
            Assert.That(castPoint.y, Is.EqualTo(expected.y).Within(Tolerance), aim.ToString());
        }
    }

    [Test]
    public void TryResolvePose_ForAnAuthoredPose_MatchesTheDirectionOverloadBitForBit()
    {
        foreach (string asset in new[] { "LongBowBowShotAttackVfx", "MagicWandCastFlashAttackVfx", "RapierThrustAttackVfx" })
        {
            AttackVfxDefinition vfx = AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(Definitions + asset + ".asset");
            for (int direction = 0; direction < 6; direction++)
            {
                Assert.That(vfx.TryResolvePose(direction, 1.25f, out AttackVfxDefinition.ResolvedPose byIndex), Is.True);
                Assert.That(vfx.TryResolvePose(vfx.GetPose(direction), 1.25f, out AttackVfxDefinition.ResolvedPose byPose), Is.True);

                Assert.That(byPose.Position, Is.EqualTo(byIndex.Position), asset + direction);
                Assert.That(byPose.Rotation, Is.EqualTo(byIndex.Rotation), asset + direction);
                Assert.That(byPose.Scale, Is.EqualTo(byIndex.Scale), asset + direction);
                Assert.That(byPose.SortingOrder, Is.EqualTo(byIndex.SortingOrder), asset + direction);
            }
        }
    }

    private static AttackVfxDefinition LongBowVfx() =>
        AssetDatabase.LoadAssetAtPath<AttackVfxDefinition>(Definitions + "LongBowBowShotAttackVfx.asset");
}
