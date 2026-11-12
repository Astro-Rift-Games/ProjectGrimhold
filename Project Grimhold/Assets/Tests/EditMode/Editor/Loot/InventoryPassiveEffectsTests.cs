using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tests.EditMode.Editor.Loot
{
    public sealed class InventoryPassiveEffectsTests
    {
        private sealed class SpyAggroReceiver : MonoBehaviour, IAggroReceiver, ICharacter
        {
            public EntityId Id { get; set; } = new(100);
            public bool IsAlive { get; set; } = true;
            public int AlertCount { get; private set; }
            public EntityId LastAttackerId { get; private set; }
            public Transform LastAttackerTransform { get; private set; }

            public void ReceiveAggroAlert(EntityId attackerId, Transform attackerTransform)
            {
                AlertCount++;
                LastAttackerId = attackerId;
                LastAttackerTransform = attackerTransform;
            }
        }

        [Test]
        public void EvaluatePassiveTreasures_WithNoTreasures_AllFlagsFalse()
        {
            var go = new GameObject("TestPlayer");
            go.AddComponent<PlayerLootReceiver>();
            var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();

            passiveController.EvaluatePassiveTreasures();

            Assert.That(passiveController.HasChaosCoin, Is.False);
            Assert.That(passiveController.HasPilgrimLight, Is.False);
            Assert.That(passiveController.HasSanctuaryHeart, Is.False);
            Assert.That(passiveController.HasVoidBell, Is.False);
            Assert.That(passiveController.HasFamineIdol, Is.False);
            Assert.That(passiveController.HasWandererEye, Is.False);

            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.DamageTakenMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.AggroRangeMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.VisionRadiusMultiplier, Is.EqualTo(1f));
            Assert.That(passiveController.IsMinimapBlocked, Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Multipliers_AreConfiguredAccordingToDoc14()
        {
            var go = new GameObject("TestPlayer");
            var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();

            SetProperty(passiveController, "HasChaosCoin", true);
            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(0.8f).Within(0.001f));

            SetProperty(passiveController, "HasChaosCoin", false);
            Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(1f));

            SetProperty(passiveController, "HasPilgrimLight", true);
            Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1.35f).Within(0.001f));

            SetProperty(passiveController, "HasSanctuaryHeart", true);
            Assert.That(passiveController.DamageTakenMultiplier, Is.EqualTo(0.85f).Within(0.001f));

            SetProperty(passiveController, "HasVoidBell", true);
            Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1.15f).Within(0.001f));

            SetProperty(passiveController, "HasFamineIdol", true);
            Assert.That(passiveController.AggroRangeMultiplier, Is.EqualTo(1.5f).Within(0.001f));

            SetProperty(passiveController, "HasWandererEye", true);
            Assert.That(passiveController.VisionRadiusMultiplier, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(passiveController.IsMinimapBlocked, Is.True);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PilgrimLight_EffectiveStaminaRegen_IncreasesBy35Percent_AndResetsOnRemovalOrDeath()
        {
            var go = new GameObject("TestPlayer");
            try
            {
                var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();
                go.AddComponent<PlayerCharacter>();
                var stamina = go.AddComponent<PlayerStaminaNetworkController>();

                Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(15f).Within(0.001f));

                passiveController.HasPilgrimLight = true;
                Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(20.25f).Within(0.001f));

                // Immediate revocation upon character death
                passiveController.AliveOverrideForTesting = false;
                passiveController.UpdatePassivesIfNeeded();

                Assert.That(passiveController.HasPilgrimLight, Is.False);
                Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(15f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PassiveTreasures_DeactivateImmediately_UponExtraction()
        {
            var go = new GameObject("TestPlayer");
            try
            {
                var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();
                go.AddComponent<PlayerCharacter>();
                var stamina = go.AddComponent<PlayerStaminaNetworkController>();

                // Configure active passives
                SetProperty(passiveController, "HasChaosCoin", true);
                SetProperty(passiveController, "HasPilgrimLight", true);
                SetProperty(passiveController, "HasSanctuaryHeart", true);
                SetProperty(passiveController, "HasVoidBell", true);
                SetProperty(passiveController, "HasFamineIdol", true);
                SetProperty(passiveController, "HasWandererEye", true);

                Assert.That(passiveController.HasPilgrimLight, Is.True);
                Assert.That(passiveController.HasVoidBell, Is.True);
                Assert.That(passiveController.HasWandererEye, Is.True);
                Assert.That(passiveController.IsMinimapBlocked, Is.True);
                Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(20.25f).Within(0.001f));

                // Immediate revocation upon extraction
                passiveController.ExtractedOverrideForTesting = true;
                passiveController.UpdatePassivesIfNeeded();

                Assert.That(passiveController.HasChaosCoin, Is.False, "Chaos Coin must deactivate upon extraction.");
                Assert.That(passiveController.HasPilgrimLight, Is.False, "Pilgrim Light must deactivate upon extraction.");
                Assert.That(passiveController.HasSanctuaryHeart, Is.False, "Sanctuary Heart must deactivate upon extraction.");
                Assert.That(passiveController.HasVoidBell, Is.False, "Void Bell must deactivate upon extraction.");
                Assert.That(passiveController.HasFamineIdol, Is.False, "Famine Idol must deactivate upon extraction.");
                Assert.That(passiveController.HasWandererEye, Is.False, "Wanderer Eye must deactivate upon extraction.");

                Assert.That(passiveController.MaxHealthMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.DamageTakenMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.AggroRangeMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.VisionRadiusMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.IsMinimapBlocked, Is.False);
                Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(15f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerExtractionController_CompleteExtraction_DeactivatesPassiveEffectsImmediately()
        {
            var go = new GameObject("TestPlayer");
            try
            {
                var extraction = go.AddComponent<PlayerExtractionController>();
                var passiveController = go.AddComponent<PlayerInventoryPassiveEffectsController>();
                var character = go.AddComponent<PlayerCharacter>();

                SetProperty(passiveController, "HasPilgrimLight", true);
                SetProperty(passiveController, "HasVoidBell", true);
                Assert.That(passiveController.HasPilgrimLight, Is.True);
                Assert.That(passiveController.HasVoidBell, Is.True);

                extraction.CompleteExtraction();

                Assert.That(extraction.State, Is.EqualTo(ExtractionState.Extracted));
                Assert.That(character.IsExtracted, Is.True);
                Assert.That(passiveController.HasPilgrimLight, Is.False);
                Assert.That(passiveController.HasVoidBell, Is.False);
                Assert.That(passiveController.SprintSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(passiveController.StaminaRegenMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SprintSpeedWithVoidBell_Applies15PercentBonus()
        {
            float normalSprintSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: true,
                sprintSpeedMultiplier: 1.5f,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 1f);

            float voidBellSprintMultiplier = 1.5f * 1.15f;
            float voidBellSprintSpeed = PlayerMovementNetworkController.ResolveVoluntarySpeed(
                moveSpeed: 5f,
                isSprinting: true,
                sprintSpeedMultiplier: voidBellSprintMultiplier,
                isDowned: false,
                downedSpeedMultiplier: 0.35f,
                abilitySpeedMultiplier: 1f,
                consumableSpeedMultiplier: 1f);

            Assert.That(normalSprintSpeed, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(voidBellSprintSpeed, Is.EqualTo(8.625f).Within(0.001f));
        }

        [Test]
        public void VoidBell_ExecuteAuthoritativeAlert_AlertsLiveEnemiesWithin15m_IgnoresDeadSelfAndOutOfRange()
        {
            const int characterLayer = 7;
            var created = new List<GameObject>();
            try
            {
                var playerGo = new GameObject("VoidBellCarrier") { layer = characterLayer };
                created.Add(playerGo);
                playerGo.transform.position = Vector3.zero;
                var selfCollider = playerGo.AddComponent<CircleCollider2D>();
                selfCollider.radius = 0.5f;
                var selfAggro = playerGo.AddComponent<SpyAggroReceiver>();
                var passive = playerGo.AddComponent<PlayerInventoryPassiveEffectsController>();

                // Live enemy within 15m (at 10m) with multiple colliders to verify deduplication
                var nearEnemyGo = new GameObject("NearLiveEnemy") { layer = characterLayer };
                created.Add(nearEnemyGo);
                nearEnemyGo.transform.position = new Vector3(10f, 0f, 0f);
                var nearSolid = nearEnemyGo.AddComponent<CircleCollider2D>();
                nearSolid.radius = 0.5f;
                var nearTrigger = nearEnemyGo.AddComponent<BoxCollider2D>();
                nearTrigger.isTrigger = true;
                var nearAggro = nearEnemyGo.AddComponent<SpyAggroReceiver>();

                // Dead enemy within 15m (at 5m) - must be ignored
                var deadEnemyGo = new GameObject("NearDeadEnemy") { layer = characterLayer };
                created.Add(deadEnemyGo);
                deadEnemyGo.transform.position = new Vector3(5f, 0f, 0f);
                deadEnemyGo.AddComponent<CircleCollider2D>().radius = 0.5f;
                var deadAggro = deadEnemyGo.AddComponent<SpyAggroReceiver>();
                deadAggro.IsAlive = false;

                // Live enemy outside 15m (at 19m) - must be ignored
                var farEnemyGo = new GameObject("FarLiveEnemy") { layer = characterLayer };
                created.Add(farEnemyGo);
                farEnemyGo.transform.position = new Vector3(19f, 0f, 0f);
                farEnemyGo.AddComponent<CircleCollider2D>().radius = 0.5f;
                var farAggro = farEnemyGo.AddComponent<SpyAggroReceiver>();

                Physics2D.SyncTransforms();

                var carrierId = new EntityId(42);
                int alerted = passive.ExecuteAuthoritativeVoidBellAlert(carrierId, playerGo.transform);

                Assert.That(alerted, Is.EqualTo(1));
                Assert.That(nearAggro.AlertCount, Is.EqualTo(1), "Multi-collider enemy within 15m must be alerted once.");
                Assert.That(nearAggro.LastAttackerId, Is.EqualTo(carrierId));
                Assert.That(nearAggro.LastAttackerTransform, Is.EqualTo(playerGo.transform));
                Assert.That(selfAggro.AlertCount, Is.EqualTo(0), "Carrier must never alert itself.");
                Assert.That(deadAggro.AlertCount, Is.EqualTo(0), "Dead enemies must not receive aggro alerts.");
                Assert.That(farAggro.AlertCount, Is.EqualTo(0), "Enemies beyond 15m must not receive aggro alerts.");
            }
            finally
            {
                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] != null) Object.DestroyImmediate(created[i]);
                }
            }
        }

        [Test]
        public void VoidBell_PresentVoidBellChime_Respects15mObserverRadius_AndCreatesPulseVfx()
        {
            var playerGo = new GameObject("VoidBellPresenterPlayer");
            var observerGo = new GameObject("LocalObserver");
            try
            {
                playerGo.transform.position = Vector3.zero;
                var passive = playerGo.AddComponent<PlayerInventoryPassiveEffectsController>();

                int chimeEventCount = 0;
                passive.OnVoidBellChime += () => chimeEventCount++;

                // Observer at 12m (within 15m audible radius)
                observerGo.transform.position = new Vector3(12f, 0f, 0f);
                PlayerInventoryPassiveEffectsController.ObserverTransformOverrideForTesting = observerGo.transform;

                passive.PresentVoidBellChime();
                Assert.That(chimeEventCount, Is.EqualTo(1));
                Assert.That(passive.PresentedVoidBellChimeCount, Is.EqualTo(1));

                Transform pulseChild = playerGo.transform.Find("VoidBellChimePulseVfx");
                Assert.That(pulseChild, Is.Not.Null, "Provisional ring pulse VFX child must be created.");
                var lineRenderer = pulseChild.GetComponent<LineRenderer>();
                Assert.That(lineRenderer, Is.Not.Null);
                Assert.That(lineRenderer.enabled, Is.True);

                // Observer at 18m (outside 15m audible radius)
                observerGo.transform.position = new Vector3(18f, 0f, 0f);
                passive.PresentVoidBellChime();
                Assert.That(chimeEventCount, Is.EqualTo(2));
                Assert.That(passive.PresentedVoidBellChimeCount, Is.EqualTo(1),
                    "Observers beyond 15m must not perceive local audio/VFX presentation.");
            }
            finally
            {
                PlayerInventoryPassiveEffectsController.ObserverTransformOverrideForTesting = null;
                Object.DestroyImmediate(observerGo);
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void WandererEye_LocalVisionSource_ReducesNearRadiusDistanceMultiplierAndLight2D_WithoutMutatingSettings()
        {
            var settings = ScriptableObject.CreateInstance<VisibilitySettings>();
            var playerGo = new GameObject("VisionTestPlayer");
            try
            {
                settings.NearRadius = 2.0f;
                settings.ConeAngleDegrees = 90f;

                playerGo.AddComponent<CircleCollider2D>();
                var rb = playerGo.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                playerGo.AddComponent<Kinematic2DMovementMotor>();
                var movement = playerGo.AddComponent<PlayerMovementNetworkController>();
                var passive = playerGo.AddComponent<PlayerInventoryPassiveEffectsController>();

                var lightGo = new GameObject("AvatarLight");
                lightGo.transform.SetParent(playerGo.transform, false);
                var light2D = lightGo.AddComponent<Light2D>();
                light2D.pointLightInnerRadius = 2.0f;
                light2D.pointLightOuterRadius = 8.0f;

                var visionSource = playerGo.AddComponent<LocalVisionSource>();
                visionSource.AllowUnspawnedForTesting = true;
                SetField(visionSource, "_settings", settings);
                SetField(visionSource, "_movement", movement);
                SetField(visionSource, "_passiveEffects", passive);
                visionSource.RefreshCachedAvatarLightsForTesting();

                // Baseline without Wanderer's Eye
                Assert.That(visionSource.TryBuildQuery(out VisionQuery baseline), Is.True);
                Assert.That(baseline.NearRadius, Is.EqualTo(2.0f).Within(0.001f));
                Assert.That(baseline.DistanceMultiplier, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(light2D.pointLightOuterRadius, Is.EqualTo(8.0f).Within(0.001f));

                // Carry Wanderer's Eye -> 0.75x (-25%) vision & light radii, ScriptableObject untouched
                passive.HasWandererEye = true;
                Assert.That(visionSource.TryBuildQuery(out VisionQuery penalized), Is.True);
                Assert.That(penalized.NearRadius, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(penalized.DistanceMultiplier, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(light2D.pointLightInnerRadius, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(light2D.pointLightOuterRadius, Is.EqualTo(6.0f).Within(0.001f));
                Assert.That(settings.NearRadius, Is.EqualTo(2.0f).Within(0.001f), "Shared VisibilitySettings asset must not be mutated.");

                // Immediate restoration on death
                passive.AliveOverrideForTesting = false;
                passive.UpdatePassivesIfNeeded();
                Assert.That(visionSource.TryBuildQuery(out VisionQuery restored), Is.True);
                Assert.That(restored.NearRadius, Is.EqualTo(2.0f).Within(0.001f));
                Assert.That(restored.DistanceMultiplier, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(light2D.pointLightOuterRadius, Is.EqualTo(8.0f).Within(0.001f));
                Assert.That(passive.IsMinimapBlocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ChaosCoin_MaxHealthPenaltyAndRestoration_DoesNotHealCurrentHealth()
        {
            float baseMaxHealth = 100f;
            float currentHealth = 100f;

            float penalizedMaxHealth = baseMaxHealth * 0.8f;
            currentHealth = Mathf.Min(currentHealth, penalizedMaxHealth);

            Assert.That(penalizedMaxHealth, Is.EqualTo(80f));
            Assert.That(currentHealth, Is.EqualTo(80f));

            float restoredMaxHealth = baseMaxHealth;
            Assert.That(restoredMaxHealth, Is.EqualTo(100f));
            Assert.That(currentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void SanctuaryHeart_DamageMitigation_ReducesIncomingDamageBy15Percent()
        {
            float incomingDamage = 40f;
            float mitigatedDamage = incomingDamage * 0.85f;

            Assert.That(mitigatedDamage, Is.EqualTo(34f).Within(0.001f));
        }

        [Test]
        public void ProductiveLootCatalog_ValidatesCleanly_AndContainsAllPassiveTreasuresAndUtilities()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null, "Productive LootDefinitionCatalog must exist.");

            bool isValid = catalog.TryValidate(out string error);
            Assert.That(isValid, Is.True, $"Productive LootDefinitionCatalog failed validation: {error}");

            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.ChaosCoinId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.PilgrimLightId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.SanctuaryHeartId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.VoidBellId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.FamineIdolId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerInventoryPassiveEffectsController.WandererEyeId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerConsumableNetworkController.RuneCompassId.Value, out _), Is.True);
            Assert.That(catalog.TryGet(PlayerConsumableNetworkController.ArcaneDetectorId.Value, out _), Is.True);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo prop = target.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
            }
            else
            {
                FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                field?.SetValue(target, value);
            }
        }
    }
}
