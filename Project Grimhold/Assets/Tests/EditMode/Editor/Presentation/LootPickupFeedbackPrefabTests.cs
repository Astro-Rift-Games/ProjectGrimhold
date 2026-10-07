using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class LootPickupFeedbackPrefabTests
    {
        [TestCase("Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab")]
        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        public void ProductiveHud_HasOptionalPickupEffectsConfigured(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var presenter = prefab.GetComponentInChildren<LootHudPresenter>(true);
            Assert.That(presenter, Is.Not.Null);
            var serialized = new SerializedObject(presenter);
            var sound = serialized.FindProperty("_pickupSound");
            var clips = sound.FindPropertyRelative("_clips");
            Assert.That(clips.arraySize, Is.EqualTo(1));
            Assert.That(clips.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ImportedAssets/Free UI Click Sound Effects Pack/AUDIO/Button/SFX_UI_Button_Organic_Plastic_Thin_Generic_1.wav")));
            Assert.That(sound.FindPropertyRelative("_spatialBlend").floatValue, Is.Zero);
            Assert.That(sound.FindPropertyRelative("_loop").boolValue, Is.False);
            Assert.That(serialized.FindProperty("_toastRoot").objectReferenceValue, Is.Not.Null);
            var particles = (ParticleSystem)serialized.FindProperty("_pickupParticles").objectReferenceValue;
            Assert.That(particles, Is.Not.Null);
            Assert.That(particles.main.duration, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(particles.main.startLifetime.constant, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(particles.main.loop, Is.False);
            Assert.That(particles.main.playOnAwake, Is.False);
            Assert.That(particles.main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy));
            Assert.That(particles.emission.burstCount, Is.EqualTo(1));
            Assert.That(particles.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null);
            Assert.That(particles.GetComponent<Fusion.NetworkObject>(), Is.Null);
            Assert.That(particles.GetComponent<Collider2D>(), Is.Null);
        }
    }
}
