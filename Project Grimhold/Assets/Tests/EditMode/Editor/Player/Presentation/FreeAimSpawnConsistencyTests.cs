using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pins art and gameplay together: for every free-aim ranged weapon, the visual shot or cast origin the player sees
/// must stay near where the gameplay projectile spawns, at every aim.
/// The weapon is held by the authored hand and turned about its grip by the residual to the aim, so the visual origin
/// leaves the aim line by a sideways error that no spawn distance can remove (the horizontal aims show it most,
/// because their bucket is 45 degrees away). Each weapon therefore has its own measured maximum delta plus a small
/// margin; a regression in the rig, the residual turn or the spawn distance moves it past that bound. Retuning the
/// distance to the best value gains less than 0.04, so the distances stay near the original 0.7 gameplay feel.
/// </summary>
public sealed class FreeAimSpawnConsistencyTests
{
    private const string Definitions = "Assets/Scriptable Objects/Loot/Definitions/";
    private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const float Margin = 0.02f;

    // Measured maximum delta at the eight aims, at the release time of the weapon's own attack clip.
    private static readonly Dictionary<string, float> MeasuredMaxDelta = new Dictionary<string, float>
    {
        { "LongBow", 0.520f },
        { "CompoundBow", 0.587f },
        { "LightCrossbow", 0.343f },
        { "MagicWand", 0.557f },
        { "MagicStaff", 0.550f },
    };

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
    public void VisualShotOrigin_StaysNearTheGameplaySpawnAtEveryAim(string weaponName)
    {
        float[] deltas = Measure(weaponName, out float spawnDistance, out float bestSpawnDistance, out float bestMaxDelta);
        float maxDelta = Mathf.Max(deltas);

        Assert.That(
            maxDelta,
            Is.LessThanOrEqualTo(MeasuredMaxDelta[weaponName] + Margin),
            $"{weaponName}: spawn {spawnDistance:F4}, best spawn {bestSpawnDistance:F4} (max {bestMaxDelta:F4}), max delta {maxDelta:F4}, " +
            $"per aim [{string.Join(", ", System.Array.ConvertAll(deltas, d => d.ToString("F3")))}]");
    }

    // Visual origin minus gameplay origin at each aim, for the weapon posed by the real presenter.
    private static float[] Measure(string weaponName, out float spawnDistance, out float bestSpawnDistance, out float bestMaxDelta)
    {
        WeaponDefinition weapon = Load(weaponName);
        var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"{Definitions}{weaponName}.asset");
        Assert.That(loot, Is.Not.Null, weaponName);
        Assert.That(weapon.Presentation.AimMode, Is.EqualTo(WeaponAimMode.FreeAim), weaponName);
        spawnDistance = weapon.HasProjectileSpawnDistance
            ? weapon.ProjectileSpawnDistance
            : ((RangedAttackConfig)weapon.PrimaryAttack).ProjectileSpawnOffset;
        AttackVfxDefinition vfx = weapon.Presentation.AttackVfx;

        GameObject player = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        player.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var presenter = player.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var view = player.GetComponentInChildren<PlayerAnimatorView>(true);
            Animator animator = view.GetComponent<Animator>();
            Transform visualRoot = animator.transform;
            typeof(PlayerWeaponPresenter).GetMethod("CacheDependencies", Private).Invoke(presenter, null);
            typeof(PlayerWeaponPresenter).GetMethod("CaptureBaseState", Private).Invoke(presenter, null);
            typeof(PlayerWeaponPresenter).GetMethod("ApplyMainHandDefinition", Private)
                .Invoke(presenter, new object[] { loot });
            var pivot = (Transform)typeof(PlayerWeaponPresenter).GetField("_mainHandWeaponPivot", Private)
                .GetValue(presenter);
            Vector2 attackOrigin = AttackOriginInVisualRoot(player, visualRoot);

            var originsAlongAim = new List<(Vector2 visual, Vector2 aim)>();
            foreach (Vector2 aim in Aims)
            {
                CharacterVisualDirection bucket = CharacterVisualDirectionResolver.Resolve(aim);
                Vector2 bucketFacing = CharacterVisualDirectionResolver.GetCanonicalVector(bucket);
                weapon.Presentation.GetAttackClip(BucketIndex(bucket))
                    .SampleAnimation(animator.gameObject, weapon.AttackReleaseSeconds);
                typeof(CharacterAnimatorView).GetProperty("VisualFacingDirection").SetValue(view, bucketFacing);
                typeof(PlayerWeaponPresenter).GetMethod("PoseWeapon", Private)
                    .Invoke(presenter, new object[] { bucketFacing, true, aim.normalized });

                Vector3 anchor = visualRoot.InverseTransformPoint(pivot.position);
                Vector3 axis = visualRoot.InverseTransformDirection(pivot.right);
                AttackVfxDefinition.DirectionalPose freePose = FreeAimAttackVfxPose.Resolve(
                    new Vector2(anchor.x, anchor.y),
                    Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg,
                    PlayerWeaponPresentationMath.ShouldMirror(bucketFacing),
                    vfx.GetPose(3),
                    vfx.GetPose(3).SortingOrder);
                Assert.That(vfx.TryResolvePose(freePose, weapon.Presentation.BladeReach,
                    out AttackVfxDefinition.ResolvedPose resolved), Is.True, weaponName);
                originsAlongAim.Add((VisualOrigin(vfx, resolved), aim.normalized));
            }

            var deltas = new float[Aims.Length];
            for (int i = 0; i < deltas.Length; i++)
            {
                (Vector2 visual, Vector2 aim) = originsAlongAim[i];
                deltas[i] = Vector2.Distance(visual, attackOrigin + aim * spawnDistance);
            }

            bestSpawnDistance = BestSpawnDistance(attackOrigin, originsAlongAim, out bestMaxDelta);
            return deltas;
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    // The distance that minimizes the largest delta, found by a fine scan.
    private static float BestSpawnDistance(
        Vector2 attackOrigin, List<(Vector2 visual, Vector2 aim)> origins, out float bestMax)
    {
        float best = 0f;
        bestMax = float.MaxValue;
        for (float distance = 0f; distance <= 2.5f; distance += 0.001f)
        {
            float max = 0f;
            foreach ((Vector2 visual, Vector2 aim) in origins)
            {
                max = Mathf.Max(max, Vector2.Distance(visual, attackOrigin + aim * distance));
            }

            if (max < bestMax)
            {
                bestMax = max;
                best = distance;
            }
        }

        return best;
    }

    private static int BucketIndex(CharacterVisualDirection direction) => direction switch
    {
        CharacterVisualDirection.North => 0,
        CharacterVisualDirection.NorthEast => 1,
        CharacterVisualDirection.NorthWest => 2,
        CharacterVisualDirection.South => 3,
        CharacterVisualDirection.SouthEast => 4,
        _ => 5
    };

    private static Vector2 VisualOrigin(AttackVfxDefinition vfx, AttackVfxDefinition.ResolvedPose resolved)
    {
        if (vfx.Visual is BowShotVfxVisualDefinition bowShot)
        {
            return (Vector2)(resolved.Position + resolved.Rotation * Vector3.Scale((Vector3)bowShot.Origin, resolved.Scale));
        }

        Assert.That(vfx.Visual, Is.InstanceOf<CastFlashVfxVisualDefinition>());
        return (Vector2)(resolved.Position + (Vector3)((CastFlashVfxVisualDefinition)vfx.Visual).Center);
    }

    private static WeaponDefinition Load(string name)
    {
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{Definitions}{name}WeaponDefinition.asset");
        Assert.That(weapon, Is.Not.Null, name);
        return weapon;
    }

    private static Vector2 AttackOriginInVisualRoot(GameObject player, Transform visualRoot)
    {
        var combat = player.GetComponentInChildren<PlayerCombatNetworkController>(true);
        var field = typeof(PlayerCombatNetworkController).GetField("_attackOrigin", Private);
        Transform attackOrigin = (Transform)field.GetValue(combat);
        return visualRoot.InverseTransformPoint(attackOrigin.position);
    }
}
