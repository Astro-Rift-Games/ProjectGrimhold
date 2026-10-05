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
        private const string OptionsViewPath = "Assets/Prefabs/UI/PlayerUI/TownOptions.prefab";

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
        public void OptionsViewPrefab_HasLogoutAndExitButtons()
        {
            var view = AssetDatabase.LoadAssetAtPath<TownOptionsView>(OptionsViewPath);
            Assert.That(view, Is.Not.Null);

            var serialized = new SerializedObject(view);
            Assert.That(serialized.FindProperty("_logoutButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_exitButton").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void SocialPlayer_WiresTheOptionsTabThroughTheBinder()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            TownOptionsPresenter[] options = social.GetComponentsInChildren<TownOptionsPresenter>(true);
            Assert.That(options, Has.Length.EqualTo(1));
            Assert.That(
                new SerializedObject(options[0]).FindProperty("_viewPrefab").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TownOptionsView>(OptionsViewPath)));

            var binder = social.GetComponentInChildren<TownInventoryBinder>(true);
            Assert.That(
                new SerializedObject(binder).FindProperty("_optionsPresenter").objectReferenceValue,
                Is.SameAs(options[0]));
        }

        [Test]
        public void SocialPlayer_AttributesPresenterSharesTheLootCatalogWithTheInventoryBinder()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            var binder = social.GetComponentInChildren<TownInventoryBinder>(true);
            var attributes = social.GetComponentInChildren<TownAttributeAssignmentPresenter>(true);

            Object binderCatalog = new SerializedObject(binder).FindProperty("_lootCatalog").objectReferenceValue;
            SerializedProperty attributesCatalog = new SerializedObject(attributes).FindProperty("_lootCatalog");

            Assert.That(binderCatalog, Is.Not.Null);
            Assert.That(attributesCatalog, Is.Not.Null, "The attributes presenter needs the catalog to resolve equipment.");
            Assert.That(attributesCatalog.objectReferenceValue, Is.SameAs(binderCatalog));
        }

        [Test]
        public void TownScene_NoLongerHasTheStandalonePauseMenu()
        {
            string scene = System.IO.File.ReadAllText("Assets/Scenes/Lobby-Town.unity");

            Assert.That(scene, Does.Not.Contain("TownPauseMenu"));
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
