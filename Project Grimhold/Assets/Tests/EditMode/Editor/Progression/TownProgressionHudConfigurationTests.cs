#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Progression
{
    public sealed class TownProgressionHudConfigurationTests
    {
        private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
        private const string ViewPath = "Assets/Resources/TownProgressionView.prefab";

        [Test]
        public void SocialPlayer_HasExactlyOneNetworkedTownProgressionPresenter()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);

            Assert.That(prefab, Is.Not.Null);
            TownProgressionPresenter[] presenters =
                prefab.GetComponentsInChildren<TownProgressionPresenter>(true);
            Assert.That(presenters, Has.Length.EqualTo(1));

            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(presenters[0]));
        }

        [Test]
        public void ViewPrefab_HasAllRequiredSerializedReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);

            Assert.That(prefab, Is.Not.Null);
            TownProgressionView view = prefab.GetComponent<TownProgressionView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.LevelText, Is.Not.Null);
            Assert.That(view.StatusText, Is.Not.Null);
            Assert.That(view.ProgressFill, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
        }
    }
}
#endif
