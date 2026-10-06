#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilitiesTabConfigurationTests
    {
        private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
        private const string ViewPath = "Assets/Prefabs/UI/PlayerUI/TownAbilities.prefab";
        private const string CatalogPath = "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset";

        [Test]
        public void ViewPrefab_HasEveryRequiredReference()
        {
            var view = AssetDatabase.LoadAssetAtPath<TownAbilitiesView>(ViewPath);
            Assert.That(view, Is.Not.Null, "TownAbilities prefab must exist with a TownAbilitiesView root.");

            var serialized = new SerializedObject(view);
            foreach (string field in new[]
                     {
                         "_contentRoot", "_unavailableNote", "_listNote", "_cardsRoot", "_cardTemplate",
                         "_detailsRoot", "_detailsNote", "_detailIcon", "_detailIconPlaceholder",
                         "_detailNameText", "_detailSubtitleText", "_detailDescriptionText",
                         "_requirementValueText", "_resourceValueText", "_cooldownValueText",
                         "_requirementRowsRoot", "_requirementRowTemplate", "_requirementsNote", "_lockedNote"
                     })
            {
                Assert.That(
                    serialized.FindProperty(field).objectReferenceValue,
                    Is.Not.Null,
                    $"{field} must be assigned on the TownAbilities prefab.");
            }

            foreach (string array in new[]
                     {
                         "_filterButtons", "_equipButtons", "_equipLabels", "_slotViews", "_previewIcons",
                         "_previewKeyTexts"
                     })
            {
                SerializedProperty property = serialized.FindProperty(array);
                int expected = array == "_filterButtons" ? 3 : 2;
                Assert.That(property.arraySize, Is.EqualTo(expected), $"{array} must hold {expected} entries.");
                for (int index = 0; index < property.arraySize; index++)
                {
                    Assert.That(
                        property.GetArrayElementAtIndex(index).objectReferenceValue,
                        Is.Not.Null,
                        $"{array}[{index}] must be assigned.");
                }
            }
        }

        [Test]
        public void ViewPrefab_TemplatesAreInactiveAndTheRootHasNoCanvas()
        {
            var view = AssetDatabase.LoadAssetAtPath<TownAbilitiesView>(ViewPath);
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);

            var card = (Component)serialized.FindProperty("_cardTemplate").objectReferenceValue;
            var row = (Component)serialized.FindProperty("_requirementRowTemplate").objectReferenceValue;
            Assert.That(card.gameObject.activeSelf, Is.False, "The card template must stay inactive.");
            Assert.That(row.gameObject.activeSelf, Is.False, "The requirement row template must stay inactive.");
            Assert.That(
                view.GetComponent<Canvas>(),
                Is.Null,
                "A Canvas-root prefab is saved with a zero scale and is invisible when hosted.");
        }

        [Test]
        public void SocialPlayer_WiresTheAbilitiesTabThroughTheBinder()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            TownAbilitiesPresenter[] abilities = social.GetComponentsInChildren<TownAbilitiesPresenter>(true);
            Assert.That(abilities, Has.Length.EqualTo(1));

            var serialized = new SerializedObject(abilities[0]);
            Assert.That(
                serialized.FindProperty("_viewPrefab").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<TownAbilitiesView>(ViewPath)));
            Assert.That(
                serialized.FindProperty("_abilityCatalog").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(CatalogPath)));

            var binder = social.GetComponentInChildren<TownInventoryBinder>(true);
            Assert.That(
                new SerializedObject(binder).FindProperty("_abilitiesPresenter").objectReferenceValue,
                Is.SameAs(abilities[0]));
        }
    }
}
#endif
