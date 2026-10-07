#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class LootPickupFeedbackTests
    {
        private GameObject _hud;
        private GameObject _player;
        private GameObject _ownedAudioManager;
        private LootHudPresenter _presenter;
        private PlayerLootReceiver _receiver;
        private LootDefinition _definition;
        private AudioClip _testClip;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab"));
            _presenter = _hud.GetComponentInChildren<LootHudPresenter>(true);
            _player = new GameObject("PickupFeedbackReceiver");
            _player.transform.position = new Vector3(4f, 5f, 0f);
            _receiver = _player.AddComponent<PlayerLootReceiver>();
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog.TryGetByIndex(0, out _definition), Is.True);
            typeof(PlayerLootReceiver).GetField("_lootCatalog", PrivateInstance).SetValue(_receiver, catalog);

            // Exercise presentation without reading unspawned Fusion state.
            Write("_lootReceiver", _receiver);
            Write("_isBound", true);
            Write("_lastGrantSequence", 5);
            _receiver.LootGranted += (Action<LootGrantPresentationEvent>)Delegate.CreateDelegate(
                typeof(Action<LootGrantPresentationEvent>), _presenter,
                typeof(LootHudPresenter).GetMethod("OnLootGranted", PrivateInstance));
            Write("_pickupSound", default(CustomClip));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps.name == "PickupSuccessParticles(Clone)") Object.DestroyImmediate(ps.gameObject);
            }
            if (_testClip != null && AudioManager.Instance != null)
            {
                foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>())
                    if (source.clip == _testClip) { source.Stop(); source.clip = null; }
            }
            Object.DestroyImmediate(_hud);
            Object.DestroyImmediate(_player);
            if (_ownedAudioManager != null) Object.DestroyImmediate(_ownedAudioManager);
            if (_testClip != null) Object.DestroyImmediate(_testClip);
        }

        [Test]
        public void ConfirmedPickup_PlaysOnceAtReceiverPosition_AndNextSequencePlaysAgain()
        {
            Publish(6);
            Assert.That(ParticleCount(), Is.EqualTo(1));
            var effect = FindParticle();
            Assert.That(effect.transform.position, Is.EqualTo(_player.transform.position));
            Assert.That(effect.transform.parent, Is.Null);
            Publish(6);
            Assert.That(ParticleCount(), Is.EqualTo(1));
            Publish(7);
            Assert.That(ParticleCount(), Is.EqualTo(2));
        }

        [Test]
        public void MissingEffects_KeepConfirmedToast()
        {
            Write("_pickupParticles", null);
            Publish(6);
            Assert.That(Read<GameObject>("_toastRoot").activeSelf, Is.True);
            Assert.That(Read<TMPro.TMP_Text>("_toastText").text,
                Does.Contain($"+2 {_definition.DisplayName}"));
            Assert.That(ParticleCount(), Is.Zero);
        }

        [Test]
        public void BaselineAndUnbind_DoNotReplayFeedback()
        {
            Publish(5);
            Assert.That(ParticleCount(), Is.Zero);
            Publish(6);
            Assert.That(ParticleCount(), Is.EqualTo(1));
            _presenter.Unbind();
            Publish(7);
            Assert.That(ParticleCount(), Is.EqualTo(1));
            Assert.That(Read<GameObject>("_toastRoot").activeSelf, Is.False);
        }

        [Test]
        public void ConfirmedPickupSound_IsDeduplicatedWithItsVisual()
        {
            if (AudioManager.Instance == null)
            {
                _ownedAudioManager = new GameObject("PickupFeedbackAudio");
                _ownedAudioManager.AddComponent<AudioManager>();
            }
            _testClip = AudioClip.Create("PickupFeedbackTestClip", 44100, 1, 44100, false);
            object boxed = default(CustomClip);
            typeof(CustomClip).GetField("_clips", PrivateInstance).SetValue(boxed, new[] { _testClip });
            Write("_pickupSound", (CustomClip)boxed);
            Publish(6);
            Assert.That(ConfiguredSoundCount(), Is.EqualTo(1));
            Publish(6);
            Assert.That(ConfiguredSoundCount(), Is.EqualTo(1));
            Publish(7);
            Assert.That(ConfiguredSoundCount(), Is.EqualTo(2));
        }

        private void Publish(int sequence)
        {
            var callback = (Action<LootGrantPresentationEvent>)typeof(PlayerLootReceiver)
                .GetField("LootGranted", PrivateInstance).GetValue(_receiver);
            callback?.Invoke(new LootGrantPresentationEvent(sequence, new EntityId(12),
                new EntityId(34), _definition.LootId, 2, 100));
        }

        private void Write(string name, object value) => typeof(LootHudPresenter)
            .GetField(name, PrivateInstance).SetValue(_presenter, value);
        private T Read<T>(string name) => (T)typeof(LootHudPresenter)
            .GetField(name, PrivateInstance).GetValue(_presenter);

        private static int ParticleCount()
        {
            int count = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.name == "PickupSuccessParticles(Clone)") count++;
            return count;
        }

        private static ParticleSystem FindParticle()
        {
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                if (ps.name == "PickupSuccessParticles(Clone)") return ps;
            Assert.Fail("Expected a standalone pickup effect.");
            return null;
        }

        private int ConfiguredSoundCount()
        {
            int count = 0;
            foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>())
                if (source.clip == _testClip) count++;
            return count;
        }
    }
}
#endif
