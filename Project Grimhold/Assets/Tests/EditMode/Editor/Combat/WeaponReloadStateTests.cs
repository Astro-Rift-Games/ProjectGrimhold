#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class WeaponReloadStateTests
    {
        [Test]
        public void MarkShot_UnloadsAndClearsAnyReloadProgress()
        {
            WeaponReloadState state = default;
            state.MarkShot();
            state.Advance(100, 0.02f, 1f, true);
            state.MarkShot();
            AssertUnloadedWithoutProgress(state);
        }

        [Test]
        public void Reload_CompletesExactlyAtRoundedDeadline_AndSnapshotRetainsProgress()
        {
            WeaponReloadState state = default;
            state.MarkShot();
            state.Advance(100, 0.02f, 0.401f, true);
            Assert.That((bool)state.Unloaded, Is.True);
            Assert.That((bool)state.Reloading, Is.True);
            Assert.That(state.ReloadEndTick, Is.EqualTo(121));
            WeaponReloadState restored = state;
            restored.Advance(120, 0.02f, 0.401f, true);
            Assert.That((bool)restored.Unloaded, Is.True);
            restored.Advance(121, 0.02f, 0.401f, true);
            AssertLoaded(restored);
        }

        [Test]
        public void Interruption_DiscardsPartialProgress_AndRestartsFromZero()
        {
            WeaponReloadState state = default;
            state.MarkShot();
            state.Advance(100, 0.02f, 1f, true);
            state.Advance(125, 0.02f, 1f, false);
            AssertUnloadedWithoutProgress(state);
            state.Advance(200, 0.02f, 1f, false);
            AssertUnloadedWithoutProgress(state);
            state.Advance(210, 0.02f, 1f, true);
            Assert.That(state.ReloadEndTick, Is.EqualTo(260));
            state.Advance(259, 0.02f, 1f, true);
            Assert.That((bool)state.Unloaded, Is.True);
            state.Advance(260, 0.02f, 1f, true);
            AssertLoaded(state);
        }

        [Test]
        public void LoadedWeapon_StaysLoadedWhileStowedOrOtherwiseNotAllowed()
        {
            WeaponReloadState state = default;
            state.Advance(100, 0.02f, 1f, false);
            AssertLoaded(state);
            state.Advance(200, 0.02f, 1f, true);
            AssertLoaded(state);
        }

        [Test]
        public void ZeroReloadSeconds_LoadsOnStartTick()
        {
            WeaponReloadState state = default;
            state.MarkShot();
            state.Advance(100, 0.02f, 0f, true);
            AssertLoaded(state);
        }

        [TestCase(-1f, 0.02f)]
        [TestCase(float.NaN, 0.02f)]
        [TestCase(float.PositiveInfinity, 0.02f)]
        [TestCase(float.NegativeInfinity, 0.02f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, -0.02f)]
        [TestCase(1f, float.NaN)]
        [TestCase(1f, float.PositiveInfinity)]
        public void InvalidTiming_DoesNotLoadOrStartReload(float seconds, float deltaTime)
        {
            WeaponReloadState state = default;
            state.MarkShot();
            state.Advance(100, deltaTime, seconds, true);
            state.Advance(200, deltaTime, seconds, true);
            AssertUnloadedWithoutProgress(state);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void WeaponDefinition_RejectsInvalidReloadTime(float seconds)
        {
            WeaponDefinition source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/LightCrossbowWeaponDefinition.asset");
            Assert.That(source, Is.Not.Null);
            WeaponDefinition weapon = Object.Instantiate(source);
            try
            {
                var serialized = new SerializedObject(weapon);
                serialized.FindProperty("_reloadSeconds").floatValue = seconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(weapon.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("reload time"));
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }

        [Test]
        public void Crossbow_SeparatesReloadTimeFromShotInterval_AndOtherWeaponsRemainValid()
        {
            WeaponDefinition crossbow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/LightCrossbowWeaponDefinition.asset");
            Assert.That(crossbow.UsesReload, Is.True);
            Assert.That(crossbow.ReloadSeconds, Is.EqualTo(1.3f));
            Assert.That(crossbow.AttackIntervalSeconds, Is.EqualTo(1.6f));
            Assert.That(crossbow.TryValidate(out string error), Is.True, error);
            LootDefinition sword = AssetDatabase.LoadAssetAtPath<LootDefinition>(
                "Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            Assert.That(sword.WeaponDefinition.UsesReload, Is.False);
            Assert.That(sword.WeaponDefinition.TryValidate(out error), Is.True, error);
        }

        private static void AssertUnloadedWithoutProgress(WeaponReloadState state)
        {
            Assert.That((bool)state.Unloaded, Is.True);
            Assert.That((bool)state.Reloading, Is.False);
            Assert.That(state.ReloadEndTick, Is.Zero);
        }

        private static void AssertLoaded(WeaponReloadState state)
        {
            Assert.That((bool)state.Unloaded, Is.False);
            Assert.That((bool)state.Reloading, Is.False);
            Assert.That(state.ReloadEndTick, Is.Zero);
        }
    }
}
#endif
