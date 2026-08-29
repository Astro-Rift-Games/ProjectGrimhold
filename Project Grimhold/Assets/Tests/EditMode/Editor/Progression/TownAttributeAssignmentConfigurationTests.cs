#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Progression
{
    public sealed class TownAttributeAssignmentConfigurationTests
    {
        private const string InputActionsPath = "Assets/Input/PlayerInputActions.inputactions";
        private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
        private const string RaidParticipantPath = "Assets/Prefabs/NetworkRaidParticipant.prefab";
        private const string ViewPath = "Assets/Resources/TownAttributeAssignmentView.prefab";

        [Test]
        public void LocalUI_HasToggleAttributesBoundToC()
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(asset, Is.Not.Null);
            InputAction action = asset.FindAction("LocalUI/ToggleAttributes", true);

            Assert.That(action.bindings, Has.Count.EqualTo(1));
            Assert.That(action.bindings[0].path, Is.EqualTo("<Keyboard>/c"));
        }

        [Test]
        public void ViewPrefab_HasSixCompleteUniqueAuthoredRows()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
            Assert.That(prefab, Is.Not.Null);
            TownAttributeAssignmentView view = prefab.GetComponent<TownAttributeAssignmentView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(view.AvailablePointsText, Is.Not.Null);
            Assert.That(view.CloseButton, Is.Not.Null);
            Assert.That(view.Rows.Count, Is.EqualTo(6));

            var attributes = new HashSet<CharacterAttribute>();
            foreach (TownAttributeAssignmentRowView row in view.Rows)
            {
                Assert.That(row, Is.Not.Null);
                Assert.That(row.LabelText, Is.Not.Null);
                Assert.That(row.ValueText, Is.Not.Null);
                Assert.That(row.AddButton, Is.Not.Null);
                Assert.That(attributes.Add(row.Attribute), Is.True);
            }

            Assert.That(attributes, Is.EquivalentTo(System.Enum.GetValues(typeof(CharacterAttribute))));
        }

        [Test]
        public void Presenter_ExistsOnlyOnceOnSocialPlayerAndNotOnRaidParticipant()
        {
            GameObject social = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            GameObject raid = AssetDatabase.LoadAssetAtPath<GameObject>(RaidParticipantPath);
            Assert.That(social, Is.Not.Null);
            Assert.That(raid, Is.Not.Null);

            TownAttributeAssignmentPresenter[] socialPresenters =
                social.GetComponentsInChildren<TownAttributeAssignmentPresenter>(true);
            Assert.That(socialPresenters, Has.Length.EqualTo(1));
            NetworkObject networkObject = social.GetComponent<NetworkObject>();
            Assert.That(networkObject, Is.Not.Null);
            Assert.That(networkObject.NetworkedBehaviours, Does.Contain(socialPresenters[0]));

            Assert.That(
                raid.GetComponentsInChildren<TownAttributeAssignmentPresenter>(true),
                Is.Empty);
        }
    }
}
#endif
