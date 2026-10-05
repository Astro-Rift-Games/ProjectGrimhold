#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Presentation
{
    public sealed class TownPlayerMenuConfigurationTests
    {
        private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
        private const string RaidParticipantPath = "Assets/Prefabs/NetworkRaidParticipant.prefab";
        private const string MenuViewPath = "Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab";

        [Test]
        public void MenuViewPrefab_HasWindowTabBarContentAndTemplate()
        {
            var view = AssetDatabase.LoadAssetAtPath<TownPlayerMenuView>(MenuViewPath);
            Assert.That(view, Is.Not.Null);

            var serialized = new SerializedObject(view);
            foreach (string field in new[]
                     {
                         "_window", "_contentRoot", "_tabBar", "_tabButtonTemplate", "_closeButton", "_titleText"
                     })
            {
                Assert.That(
                    serialized.FindProperty(field).objectReferenceValue,
                    Is.Not.Null,
                    $"{field} must be assigned on the TownPlayerMenu prefab.");
            }
        }

        [Test]
        public void SocialPlayer_WiresOneMenuPresenterToBinderAndAttributePresenter()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            Assert.That(social, Is.Not.Null);

            TownPlayerMenuPresenter[] menus = social.GetComponentsInChildren<TownPlayerMenuPresenter>(true);
            Assert.That(menus, Has.Length.EqualTo(1));
            Assert.That(
                new SerializedObject(menus[0]).FindProperty("_viewPrefab").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TownPlayerMenuView>(MenuViewPath)));

            var binder = social.GetComponentInChildren<TownInventoryBinder>(true);
            Assert.That(binder, Is.Not.Null);
            Assert.That(
                new SerializedObject(binder).FindProperty("_menuPresenter").objectReferenceValue,
                Is.SameAs(menus[0]));

            var attributes = social.GetComponentInChildren<TownAttributeAssignmentPresenter>(true);
            Assert.That(attributes, Is.Not.Null);
            Assert.That(
                new SerializedObject(attributes).FindProperty("_menu").objectReferenceValue,
                Is.SameAs(menus[0]));
        }

        [Test]
        public void SocialPlayer_MenuControlsTownHudAndProgressionRegistersItsView()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            var menu = social.GetComponentInChildren<TownPlayerMenuPresenter>(true);
            Assert.That(menu, Is.Not.Null);

            SerializedProperty hudRoots = new SerializedObject(menu).FindProperty("_hudRoots");
            Assert.That(hudRoots, Is.Not.Null, "The menu must expose the HUD roots it hides while open.");
            var hudObjects = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < hudRoots.arraySize; i++)
            {
                hudObjects.Add((GameObject)hudRoots.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            Assert.That(hudObjects, Has.Member(social.GetComponentInChildren<TownPartyHudView>(true).gameObject));
            Assert.That(hudObjects, Has.Member(social.GetComponentInChildren<InteractionHudPresenter>(true).gameObject));

            var progression = social.GetComponentInChildren<TownProgressionPresenter>(true);
            Assert.That(progression, Is.Not.Null);
            Assert.That(
                new SerializedObject(progression).FindProperty("_menu").objectReferenceValue,
                Is.SameAs(menu));
        }

        [Test]
        public void RaidParticipant_DoesNotCarryTheTownMenu()
        {
            GameObject raid = AssetDatabase.LoadAssetAtPath<GameObject>(RaidParticipantPath);
            Assert.That(raid, Is.Not.Null);
            Assert.That(raid.GetComponentsInChildren<TownPlayerMenuPresenter>(true), Is.Empty);
        }
    }
}
#endif
