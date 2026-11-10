using System.Collections.Generic;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Enemies.Specter;
using UnityEngine;

namespace Tests.EditMode.Enemies
{
    [TestFixture]
    public sealed class SpecterRulesAndCombatTests
    {
        [TestCase(SpecterStateType.Reposo, true, false)]
        [TestCase(SpecterStateType.Persecucion, true, false)]
        [TestCase(SpecterStateType.Canalizacion, false, true)]
        [TestCase(SpecterStateType.Detonacion, false, true)]
        [TestCase(SpecterStateType.Recuperacion, false, true)]
        [TestCase(SpecterStateType.Desvanecimiento, false, false)]
        [TestCase(SpecterStateType.Desvanecido, false, false)]
        [TestCase(SpecterStateType.Reaparicion, true, false)]
        [TestCase(SpecterStateType.Muerte, false, false)]
        public void StateClassifications_MatchDesignSpec(SpecterStateType state, bool expectedIncorporeal, bool expectedManifested)
        {
            Assert.That(SpecterRules.IsIncorporeal(state), Is.EqualTo(expectedIncorporeal));
            Assert.That(SpecterRules.IsManifested(state), Is.EqualTo(expectedManifested));
        }

        [Test]
        public void PhysicalDamage_IsStrictlyZeroInIncorporealStates_AndAppliesInManifestedStates()
        {
            float amount = 50f;

            // Incorporeal states: immune to physical
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Reposo, amount, DamageType.Physical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Persecucion, amount, DamageType.Physical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Reaparicion, amount, DamageType.Physical), Is.EqualTo(0f));

            // Manifested states: vulnerable to physical (TASK-272 §3)
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Canalizacion, amount, DamageType.Physical), Is.EqualTo(amount));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Detonacion, amount, DamageType.Physical), Is.EqualTo(amount));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Recuperacion, amount, DamageType.Physical), Is.EqualTo(amount));

            // Hidden, fading or dead: 0
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Desvanecimiento, amount, DamageType.Physical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Desvanecido, amount, DamageType.Physical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Muerte, amount, DamageType.Physical), Is.EqualTo(0f));
        }

        [TestCase(SpecterStateType.Reposo)]
        [TestCase(SpecterStateType.Persecucion)]
        [TestCase(SpecterStateType.Canalizacion)]
        [TestCase(SpecterStateType.Detonacion)]
        [TestCase(SpecterStateType.Recuperacion)]
        [TestCase(SpecterStateType.Reaparicion)]
        public void MagicalDamage_AppliesInAllVisibleStates(SpecterStateType state)
        {
            float amount = 42f;
            Assert.That(SpecterRules.CalculateMitigatedDamage(state, amount, DamageType.Magical), Is.EqualTo(amount));
            Assert.That(SpecterRules.CalculateMitigatedDamage(state, amount, DamageType.TrueDamage), Is.EqualTo(amount));
        }

        [Test]
        public void MagicalDamage_IsZeroWhenDesvanecidoOrDesvanecimientoOrDead()
        {
            float amount = 42f;
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Desvanecimiento, amount, DamageType.Magical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Desvanecido, amount, DamageType.Magical), Is.EqualTo(0f));
            Assert.That(SpecterRules.CalculateMitigatedDamage(SpecterStateType.Muerte, amount, DamageType.Magical), Is.EqualTo(0f));

            Assert.That(SpecterRules.CanReceiveDamage(SpecterStateType.Desvanecimiento), Is.False);
            Assert.That(SpecterRules.CanReceiveDamage(SpecterStateType.Desvanecido), Is.False);
            Assert.That(SpecterRules.CanReceiveDamage(SpecterStateType.Muerte), Is.False);
        }

        [Test]
        public void ReactiveDisplacement_IsBlockedDuringRecoveryWindowOrWhenOnCooldown()
        {
            // During 1.5s recovery window: must conclude fully without early displacement (TASK-274 §5)
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Recuperacion, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.False);

            // While cooldown is active
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: false, isCooldownActive: true), Is.False);

            // While fatal
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: true, isCooldownActive: false), Is.False);

            // While already fading or invisible
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Desvanecimiento, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.False);
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Desvanecido, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.False);

            // Allowed in active states when cooldown ready and non-fatal
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Reposo, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.True);
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.True);
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Canalizacion, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.True);
        }

        [Test]
        public void HateList_SelectsByProximityInitially_AndByHighestAccumulatedDamageAfterDisplacement()
        {
            var hateList = new SpecterHateList();
            Vector2 specterPos = Vector2.zero;

            EntityId player1 = new EntityId(101);
            EntityId player2 = new EntityId(102);

            var candidates = new List<SpecterHateList.CandidateTarget>
            {
                new(player1, new Vector2(2f, 0f)), // distance 2
                new(player2, new Vector2(5f, 0f))  // distance 5
            };

            // 1. Initial selection: no damage -> closest player (player1)
            bool found1 = hateList.SelectTarget(candidates, specterPos, out EntityId initialTarget);
            Assert.That(found1, Is.True);
            Assert.That(initialTarget, Is.EqualTo(player1));

            // 2. Player 2 deals high magic damage (60), Player 1 deals low damage (10)
            hateList.RecordDamage(player1, 10f);
            hateList.RecordDamage(player2, 60f);

            // 3. Selection after displacement: player2 has higher accumulated damage despite being further away
            bool found2 = hateList.SelectTarget(candidates, specterPos, out EntityId postDisplacementTarget);
            Assert.That(found2, Is.True);
            Assert.That(postDisplacementTarget, Is.EqualTo(player2));

            // 4. If Player 2 extracts or dies (ineligible/removed from candidates), falls back to Player 1
            var remainingCandidates = new List<SpecterHateList.CandidateTarget>
            {
                new(player1, new Vector2(2f, 0f))
            };
            bool found3 = hateList.SelectTarget(remainingCandidates, specterPos, out EntityId fallbackTarget);
            Assert.That(found3, Is.True);
            Assert.That(fallbackTarget, Is.EqualTo(player1));
        }

        [Test]
        public void HateList_DistanceBreaksTiesForEqualDamage()
        {
            var hateList = new SpecterHateList();
            Vector2 specterPos = Vector2.zero;

            EntityId playerClose = new EntityId(101);
            EntityId playerFar = new EntityId(102);

            var candidates = new List<SpecterHateList.CandidateTarget>
            {
                new(playerClose, new Vector2(2f, 0f)),
                new(playerFar, new Vector2(6f, 0f))
            };

            hateList.RecordDamage(playerClose, 30f);
            hateList.RecordDamage(playerFar, 30f);

            bool found = hateList.SelectTarget(candidates, specterPos, out EntityId target);
            Assert.That(found, Is.True);
            Assert.That(target, Is.EqualTo(playerClose));
        }

        [Test]
        public void TeleportRules_DestinationConstrainedToAnnulusAndLeashRadius()
        {
            Vector2 targetPos = new Vector2(10f, 10f);
            Vector2 homePos = new Vector2(10f, 10f);
            float leashRadius = 15f;
            float minRadius = 3f;
            float maxRadius = 5f;

            bool found = SpecterTeleportRules.TryFindDestination(
                targetPos,
                homePos,
                leashRadius,
                minRadius,
                maxRadius,
                pos => true,
                out Vector2 destination);

            Assert.That(found, Is.True);
            float distToTarget = Vector2.Distance(destination, targetPos);
            Assert.That(distToTarget, Is.GreaterThanOrEqualTo(minRadius - 0.05f));
            Assert.That(distToTarget, Is.LessThanOrEqualTo(maxRadius + 0.05f));
            Assert.That(Vector2.Distance(destination, homePos), Is.LessThanOrEqualTo(leashRadius));
        }

        [Test]
        public void TeleportRules_FindDestination_DoesNotRequireLineOfSightFromOrigin()
        {
            Vector2 origin = Vector2.zero;
            Vector2 target = new Vector2(5f, 0f);
            Vector2 home = Vector2.zero;
            float leash = 15f;

            // Scenario: Solid wall exists right in front of origin (x=1), blocking line of sight from origin
            // But the destination point itself at x=8 (distance 3 from target) is valid and unblocked.
            Vector2 dest = SpecterTeleportRules.FindDestination(
                origin,
                target,
                home,
                leash,
                pt => pt.x > 15f); // only points beyond x=15 are blocked; destination near x=8 is valid

            Assert.That(dest, Is.Not.EqualTo(home), "Destination should be found without requiring LOS from origin.");
            Assert.That(Vector2.Distance(dest, target), Is.GreaterThanOrEqualTo(SpecterRules.MinTeleportDistance - 0.05f));
            Assert.That(Vector2.Distance(dest, target), Is.LessThanOrEqualTo(SpecterRules.MaxTeleportDistance + 0.05f));
            Assert.That(Vector2.Distance(dest, home), Is.LessThanOrEqualTo(leash));
        }

        [Test]
        public void TeleportRules_ProgressiveAnnularWidening_FindsCandidateWhenInitialBandBlocked()
        {
            Vector2 origin = Vector2.zero;
            Vector2 target = new Vector2(5f, 0f);
            Vector2 home = Vector2.zero;
            float leash = 20f;

            // Block all points in initial band [3.0, 5.0] from target
            // Allow only points with distance between 5.5 and 7.0 from target
            bool IsBlocked(Vector2 pt)
            {
                float d = Vector2.Distance(pt, target);
                return d >= 2.8f && d <= 5.2f;
            }

            Vector2 dest = SpecterTeleportRules.FindDestination(
                origin,
                target,
                home,
                leash,
                IsBlocked);

            float resultDistance = Vector2.Distance(dest, target);
            Assert.That(resultDistance, Is.GreaterThan(5.0f), "Should widen the search band to find an unblocked position.");
            Assert.That(Vector2.Distance(dest, home), Is.LessThanOrEqualTo(leash));
        }

        [Test]
        public void TeleportRules_FallbacksToHomeArea_WhenAllWidenedBandsFail()
        {
            Vector2 origin = Vector2.zero;
            Vector2 target = new Vector2(5f, 0f);
            Vector2 home = new Vector2(1f, 1f);
            float leash = 15f;

            // Block everything except home
            bool IsBlocked(Vector2 pt) => Vector2.Distance(pt, home) > 0.01f;

            Vector2 dest = SpecterTeleportRules.FindDestination(
                origin,
                target,
                home,
                leash,
                IsBlocked);

            Assert.That(dest, Is.EqualTo(home), "Should fallback to homeAreaPos when all candidate points are blocked.");
        }

        [Test]
        public void AttackOcclusion_FiltersTargetsBehindWalls()
        {
            Vector2 circleCenter = Vector2.zero;
            float radius = 1.5f;

            EntityId targetOpen = new EntityId(201);
            EntityId targetBehindWall = new EntityId(202);
            EntityId targetOutOfRange = new EntityId(203);

            var candidates = new List<(EntityId id, Vector2 position)>
            {
                (targetOpen, new Vector2(1.0f, 0f)),        // in radius, open
                (targetBehindWall, new Vector2(0f, 1.2f)),   // in radius, occluded
                (targetOutOfRange, new Vector2(2.5f, 0f))    // outside radius (2.5 > 1.5)
            };

            // Linecast obstruction check: simulates wall blocking targetBehindWall
            bool LinecastCheck(Vector2 from, Vector2 to)
            {
                return to.y > 0.5f; // Wall between y=0.5 and y=1.2
            }

            List<EntityId> affected = SpecterAttackOcclusion.ResolveAffectedTargets(
                circleCenter,
                radius,
                candidates,
                LinecastCheck);

            Assert.That(affected, Contains.Item(targetOpen));
            Assert.That(affected, Has.No.Member(targetBehindWall), "Occluded target should not be affected by damage.");
            Assert.That(affected, Has.No.Member(targetOutOfRange), "Out of range target should not be affected by damage.");
            Assert.That(affected.Count, Is.EqualTo(1));
        }

        [Test]
        public void HybridDamage_SeparatesPhysicalAndMagicalComponentsDeterministically()
        {
            float physicalAmount = 30f;
            float magicalAmount = 20f;

            // Incorporeal state (Persecucion): Physical is completely negated (0), Magical applies fully (20)
            float mitigatedPhysicalIncorporeal = SpecterRules.CalculateMitigatedDamage(SpecterStateType.Persecucion, physicalAmount, DamageType.Physical);
            float mitigatedMagicalIncorporeal = SpecterRules.CalculateMitigatedDamage(SpecterStateType.Persecucion, magicalAmount, DamageType.Magical);
            Assert.That(mitigatedPhysicalIncorporeal, Is.EqualTo(0f));
            Assert.That(mitigatedMagicalIncorporeal, Is.EqualTo(20f));
            Assert.That(mitigatedPhysicalIncorporeal + mitigatedMagicalIncorporeal, Is.EqualTo(20f));

            // Manifested state (Canalizacion): Both apply fully (30 + 20 = 50)
            float mitigatedPhysicalManifested = SpecterRules.CalculateMitigatedDamage(SpecterStateType.Canalizacion, physicalAmount, DamageType.Physical);
            float mitigatedMagicalManifested = SpecterRules.CalculateMitigatedDamage(SpecterStateType.Canalizacion, magicalAmount, DamageType.Magical);
            Assert.That(mitigatedPhysicalManifested, Is.EqualTo(30f));
            Assert.That(mitigatedMagicalManifested, Is.EqualTo(20f));
            Assert.That(mitigatedPhysicalManifested + mitigatedMagicalManifested, Is.EqualTo(50f));
        }

        [Test]
        public void ReactiveDisplacement_OnlyMagicDamageTriggers_NeverPhysicalOrFatal()
        {
            // Non-fatal magic damage triggers when cooldown ready
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.True);

            // Physical damage never triggers reactive displacement even if non-fatal
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: false, isFatal: false, isCooldownActive: false), Is.False);

            // Fatal magic damage never triggers reactive displacement (must trigger death)
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: true, isCooldownActive: false), Is.False);

            // Magic damage while on 4s cooldown never triggers reactive displacement
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Persecucion, isMagicDamage: true, isFatal: false, isCooldownActive: true), Is.False);

            // Magic damage during 1.5s recovery window never triggers reactive displacement (recovery window is uninterrupted)
            Assert.That(SpecterRules.CanTriggerReactiveDisplacement(SpecterStateType.Recuperacion, isMagicDamage: true, isFatal: false, isCooldownActive: false), Is.False);
        }

        [Test]
        public void SpecterConstants_MatchDesignSpecifications()
        {
            Assert.That(SpecterRules.ChannelDuration, Is.EqualTo(1.25f));
            Assert.That(SpecterRules.RecoveryDuration, Is.EqualTo(1.5f));
            Assert.That(SpecterRules.FadeOutDuration, Is.EqualTo(0.4f));
            Assert.That(SpecterRules.HiddenDuration, Is.EqualTo(0.5f));
            Assert.That(SpecterRules.FadeInDuration, Is.EqualTo(0.4f));
            Assert.That(SpecterRules.ReactiveDisplacementCooldown, Is.EqualTo(4.0f));
            Assert.That(SpecterRules.AttackCooldown, Is.EqualTo(3.0f));
            Assert.That(SpecterRules.DetonationRadius, Is.EqualTo(1.5f));
            Assert.That(SpecterRules.MinTeleportDistance, Is.EqualTo(3.0f));
            Assert.That(SpecterRules.MaxTeleportDistance, Is.EqualTo(5.0f));
        }

#if UNITY_EDITOR
        [Test]
        public void SpecterPrefab_TelegraphLine_HasValidUnlitMaterial()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab");
            Assert.That(prefab, Is.Not.Null, "Specter prefab must exist.");
            var lineObj = prefab.transform.Find("TelegraphLine");
            Assert.That(lineObj, Is.Not.Null, "TelegraphLine child must exist.");
            var lr = lineObj.GetComponent<LineRenderer>();
            Assert.That(lr, Is.Not.Null, "LineRenderer component must exist.");
            Assert.That(lr.sharedMaterial, Is.Not.Null, "LineRenderer must have a valid sharedMaterial assigned.");
            Assert.That(lr.sharedMaterial.shader, Is.Not.Null, "Material must have a valid shader.");
            Assert.That(lr.sharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), "Material must not use the fallback error shader.");
        }

        [Test]
        public void SpecterLootContainerContentTable_ValidatesSnapshotSuccessfully()
        {
            var table = UnityEditor.AssetDatabase.LoadAssetAtPath<LootContainerContentTable>(
                "Assets/Scriptable Objects/Loot/Tables/SpecterLootContainerContentTable.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

            Assert.That(table, Is.Not.Null, "Specter loot table asset must exist.");
            Assert.That(catalog, Is.Not.Null, "Loot definition catalog asset must exist.");

            bool valid = LootContainerContentTableValidation.TryCreateSnapshot(
                table,
                catalog,
                slotCapacity: 6,
                distinctLootCapacity: NetworkLootContainer.MaxDistinctLootTypes,
                out ValidatedLootContainerContentSnapshot snapshot,
                out string error);

            Assert.That(valid, Is.True, $"Snapshot creation failed: {error}");
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.EntryCount, Is.EqualTo(3));
            Assert.That(snapshot.MinimumDistinctStacks, Is.EqualTo(1));
            Assert.That(snapshot.MaximumDistinctStacks, Is.EqualTo(2));
            Assert.That(LootContainerContentTableValidation.HasAdditionalStackCapacity(snapshot, out string capacityError), Is.True, capacityError);
        }
#endif
    }
}
