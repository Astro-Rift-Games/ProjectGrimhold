using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Abilities
{
    public sealed class AbilityEnemyTargetMarkerTests
    {
        private static readonly EntityId CasterId = new EntityId(1);

        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab")]
        public void StunIndicator_IsBoundToOwnEffectAndVisibility(string path)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var presenter = prefab.GetComponent("StunStatusIndicatorPresenter");
            Assert.That(presenter, Is.Not.Null);
            var serialized = new UnityEditor.SerializedObject(presenter);
            var source = serialized.FindProperty("_statusSource").objectReferenceValue as Component;
            Assert.That(source.gameObject, Is.SameAs(prefab));
            Assert.That(source.GetType().GetInterface("IStunStatus"), Is.Not.Null);
            var indicator = serialized.FindProperty("_indicator").objectReferenceValue as GameObject;
            Assert.That(indicator.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(indicator.activeSelf, Is.False);
            var sprite = indicator.GetComponent<SpriteRenderer>();
            Assert.That(sprite.sprite, Is.Not.Null);
            Assert.That(sprite.color, Is.EqualTo(Color.yellow));
            var visibility = prefab.GetComponent<ProjectGrimhold.Gameplay.Visibility.EntityVisibilityPresenter>();
            var renderers = new UnityEditor.SerializedObject(visibility).FindProperty("_renderers");
            bool found = false;
            for (int i = 0; i < renderers.arraySize; i++)
                found |= renderers.GetArrayElementAtIndex(i).objectReferenceValue == sprite;
            Assert.That(found, Is.True, "Indicator shares the entity's visibility and fade.");
        }

        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemySpecter.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab")]
        [TestCase("Assets/Prefabs/Enemies/Slimes/RedSlime.prefab")]
        public void EnemyPrefab_ComposesExactlyOneRegisteredStun(string path)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var effects = prefab.GetComponents<EnemyStunNetworkEffect>();
            Assert.That(effects.Length, Is.EqualTo(1));
            var networkObject = prefab.GetComponent<Fusion.NetworkObject>();
            var serialized = new UnityEditor.SerializedObject(networkObject);
            var behaviours = serialized.FindProperty("NetworkedBehaviours");
            int occurrences = 0;
            for (int i = 0; i < behaviours.arraySize; i++)
                if (behaviours.GetArrayElementAtIndex(i).objectReferenceValue == effects[0]) occurrences++;
            Assert.That(occurrences, Is.EqualTo(1));
        }

        [Test]
        public void EnemyCharacter_ImplementsTheEnemyTargetMarker()
        {
            Assert.That(typeof(IAbilityEnemyTarget).IsAssignableFrom(typeof(EnemyCharacter)), Is.True);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Test]
        public void TrainingDummy_ImplementsTheEnemyTargetMarker()
        {
            Assert.That(typeof(IAbilityEnemyTarget).IsAssignableFrom(typeof(TrainingDummyCharacter)), Is.True);
        }

        [Test]
        public void TrainingDummy_AlwaysAcceptsDamageRequests()
        {
            var go = new GameObject("Dummy");
            try
            {
                var dummy = go.AddComponent<TrainingDummyCharacter>();
                Assert.That(dummy.CanReceiveDamage, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
#endif

        [Test]
        public void Predicate_AcceptsMarkedLivingDamageable()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2)), Is.True);
        }

        [Test]
        public void Predicate_RejectsUnmarkedDamageable()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new UnmarkedTarget(2)), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedCaster()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(1)), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedDeadTarget()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2) { IsAlive = false }), Is.False);
        }

        [Test]
        public void Predicate_RejectsMarkedTargetThatCannotReceiveDamage()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, new MarkedTarget(2) { CanReceiveDamage = false }), Is.False);
        }

        [Test]
        public void Predicate_RejectsNull()
        {
            Assert.That(AbilityTargetPredicate.IsValidEnemy(CasterId, null), Is.False);
        }

        private class UnmarkedTarget : IDamageable, ICharacter
        {
            public UnmarkedTarget(int id) { Id = new EntityId(id); }
            public EntityId Id { get; }
            public bool IsAlive { get; set; } = true;
            public bool CanReceiveDamage { get; set; } = true;

            public DamageResult ApplyDamage(in DamageRequest request) =>
                new DamageResult(Id, true, request.Amount, 100f, false, DamageFailureReason.None);
        }

        private sealed class MarkedTarget : UnmarkedTarget, IAbilityEnemyTarget
        {
            public MarkedTarget(int id) : base(id) { }
        }
    }
}
