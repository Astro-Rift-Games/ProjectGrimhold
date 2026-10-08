using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pins art and gameplay together: for every free-aim ranged weapon, the visual shot or cast origin the player
/// sees must sit where the gameplay projectile spawns, at every aim.
/// </summary>
public sealed class FreeAimSpawnConsistencyTests
{
    // World units. The two origins are derived from the same distances, so this only absorbs the sprite tip tilt.
    private const float Tolerance = 0.05f;
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";

    private static readonly string[] RangedWeapons =
        { "LongBow", "CompoundBow", "LightCrossbow", "MagicWand", "MagicStaff" };

    private static readonly Vector2[] Aims =
    {
        Vector2.right, Vector2.up, Vector2.left, Vector2.down,
        new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.8f), new Vector2(0.6f, -0.8f)
    };

    [TestCaseSource(nameof(RangedWeapons))]
    public void RangedWeapon_UsesFreeAimAndIsValid(string weaponName)
    {
        WeaponDefinition weapon = Load(weaponName);

        Assert.That(weapon.Presentation.AimMode, Is.EqualTo(WeaponAimMode.FreeAim), weaponName);
        Assert.That(weapon.TryValidate(out string error), Is.True, error);
    }

    [TestCaseSource(nameof(RangedWeapons))]
    public void VisualShotOrigin_MatchesTheGameplaySpawnAtEveryAim(string weaponName)
    {
        WeaponDefinition weapon = Load(weaponName);
        Assert.That(weapon.Presentation.AimMode, Is.EqualTo(WeaponAimMode.FreeAim), weaponName);
        Vector2 attackOrigin = AttackOriginInVisualRoot();
        float spawnDistance = SpawnDistance(weapon);
        WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
        AttackVfxDefinition vfx = presentation.AttackVfx;
        float maxDelta = 0f;

        foreach (Vector2 aim in Aims)
        {
            Vector2 anchor = FreeAimAnchor.Resolve(aim, presentation.StanceOffset);
            RangedWeaponAimPose pose = RangedWeaponAimPoseMath.Resolve(
                aim, anchor, presentation.GripPoint, presentation.SecondaryGripPoint,
                presentation.AngleCorrection, Vector2.one);
            AttackVfxDefinition.DirectionalPose freePose = FreeAimAttackVfxPose.Resolve(
                anchor, pose.PivotAngleDegrees, pose.Mirrored, vfx.GetPose(3), vfx.GetPose(3).SortingOrder);

            Assert.That(vfx.TryResolvePose(freePose, presentation.BladeReach, out AttackVfxDefinition.ResolvedPose resolved),
                Is.True, weaponName);
            Vector2 visualOrigin = VisualOrigin(vfx, resolved);
            Vector2 gameplayOrigin = attackOrigin + aim.normalized * spawnDistance;
            maxDelta = Mathf.Max(maxDelta, Vector2.Distance(visualOrigin, gameplayOrigin));
        }

        TestContext.WriteLine($"{weaponName}: spawn distance {spawnDistance:F4}, max delta {maxDelta:F4}");
        Assert.That(maxDelta, Is.LessThanOrEqualTo(Tolerance), weaponName);
    }

    private static Vector2 VisualOrigin(AttackVfxDefinition vfx, AttackVfxDefinition.ResolvedPose resolved)
    {
        if (vfx.Visual is BowShotVfxVisualDefinition bowShot)
        {
            return (Vector2)(resolved.Position + resolved.Rotation * Vector3.Scale((Vector3)bowShot.Origin, resolved.Scale));
        }

        Assert.That(vfx.Visual, Is.InstanceOf<CastFlashVfxVisualDefinition>());
        return (Vector2)(resolved.Position + (Vector3)((CastFlashVfxVisualDefinition)vfx.Visual).Center);
    }

    private static float SpawnDistance(WeaponDefinition weapon) => weapon.HasProjectileSpawnDistance
        ? weapon.ProjectileSpawnDistance
        : ((RangedAttackConfig)weapon.PrimaryAttack).ProjectileSpawnOffset;

    private static WeaponDefinition Load(string name)
    {
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{name}WeaponDefinition.asset");
        Assert.That(weapon, Is.Not.Null, name);
        return weapon;
    }

    private static Vector2 AttackOriginInVisualRoot()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var combat = root.GetComponentInChildren<PlayerCombatNetworkController>(true);
            var field = typeof(PlayerCombatNetworkController).GetField(
                "_attackOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
            Transform attackOrigin = (Transform)field.GetValue(combat);
            Transform visualRoot = root.transform.Find("VisualRoot");
            return visualRoot.InverseTransformPoint(attackOrigin.position);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
