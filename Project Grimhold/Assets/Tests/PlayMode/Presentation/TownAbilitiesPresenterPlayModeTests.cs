#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownAbilitiesPresenterPlayModeTests
    {
        private const string MenuPrefabPath = "Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab";
        private const string AbilitiesPrefabPath = "Assets/Prefabs/UI/PlayerUI/TownAbilities.prefab";
        private const string CatalogPath = "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset";
        private const string LootCatalogPath = "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset";

        private static readonly ProfileId Profile = new("95959595959595959595959595959595");
        private static readonly AbilityId Charge = new("charge");
        private static readonly AbilityId Trap = new("trap");
        private static readonly AbilityId Arcane = new("arcane_projectile");

        private GameObject _host;
        private GameObject _readerObject;
        private GameObject _canvas;
        private TownPlayerMenuPresenter _menu;
        private TownAbilitiesPresenter _abilities;
        private ApplicationStashContext _context;
        private LocalProfileStore _store;
        private bool _canMutate;

        [SetUp]
        public void SetUp()
        {
            _canMutate = true;
            _host = new GameObject("TownAbilitiesPresenterTests");
            _menu = _host.AddComponent<TownPlayerMenuPresenter>();
            SetField(_menu, "_viewPrefab", Load<TownPlayerMenuView>(MenuPrefabPath));
            _abilities = _host.AddComponent<TownAbilitiesPresenter>();
            SetField(_abilities, "_viewPrefab", Load<TownAbilitiesView>(AbilitiesPrefabPath));
            SetField(_abilities, "_abilityCatalog", Load<AbilityDefinitionCatalog>(CatalogPath));
            _context = _host.AddComponent<ApplicationStashContext>();

            _readerObject = new GameObject("TownAbilitiesInputReader");
            var reader = _readerObject.AddComponent<PlayerInputReader>();
            _canvas = new GameObject("TownAbilitiesCanvas", typeof(RectTransform), typeof(Canvas));
            _menu.Bind(reader, _canvas.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_readerObject);
            Object.DestroyImmediate(_canvas);
        }

        // Dexterity stays at 3 so Trap is unlocked but its requirement is not met.
        private void Start(params AbilityId[] unlocked)
        {
            var lootCatalog = Load<LootDefinitionCatalog>(LootCatalogPath);
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, lootCatalog), Is.True);
            _store = new LocalProfileStore(
                repository,
                Profile,
                lootCatalog: lootCatalog,
                abilityCatalog: Load<AbilityDefinitionCatalog>(CatalogPath));
            foreach (AbilityId id in unlocked)
            {
                Assert.That(_store.TryUnlockAbility(id), Is.EqualTo(AbilityUnlockResult.Success));
            }

            Assert.That(
                CharacterAttributeState.TryCreate(0, 0, 10, 3, 10, 0, 0, out CharacterAttributeState attributes),
                Is.True);
            _store.ForceCharacterAttributeState(attributes);
            _context.Initialize(_store, null, null, null, null);
            _abilities.Register(_menu, _context, () => _canMutate);
        }

        [Test]
        public void Register_AddsTheAbilitiesTab()
        {
            Start(Charge);

            OpenTab();

            Assert.That(_menu.IsOpen, Is.True);
            Assert.That(_menu.SelectedTabId, Is.EqualTo(TownMenuTabIds.Abilities));
        }

        [Test]
        public void Tab_ShowsOnlyTheUnlockedAbilitiesAsCards()
        {
            Start(Charge, Trap, Arcane);
            OpenTab();

            Assert.That(CardIds(), Is.EquivalentTo(new[] { Charge, Trap, Arcane }));
        }

        [Test]
        public void FilterButtons_NarrowTheList()
        {
            Start(Charge, Trap, Arcane);
            OpenTab();

            View.FilterButton(TownAbilitiesFilter.Stamina).onClick.Invoke();
            Assert.That(CardIds(), Is.EquivalentTo(new[] { Charge, Trap }));

            View.FilterButton(TownAbilitiesFilter.Mana).onClick.Invoke();
            Assert.That(CardIds(), Is.EquivalentTo(new[] { Arcane }));

            View.FilterButton(TownAbilitiesFilter.All).onClick.Invoke();
            Assert.That(CardIds(), Has.Count.EqualTo(3));
        }

        [Test]
        public void FilterChange_KeepsTheSelectionWhenStillVisibleOtherwiseSelectsTheFirst()
        {
            Start(Charge, Trap, Arcane);
            OpenTab();
            CardOf(Trap).Button.onClick.Invoke();

            View.FilterButton(TownAbilitiesFilter.Stamina).onClick.Invoke();
            Assert.That(CardOf(Trap).IsSelected, Is.True, "Trap is still visible, so it stays selected.");

            View.FilterButton(TownAbilitiesFilter.Mana).onClick.Invoke();
            Assert.That(CardOf(Arcane).IsSelected, Is.True, "Trap left the list, so the first visible is selected.");
        }

        [Test]
        public void Cards_ShowTheStateBadgeAndSubtitle()
        {
            Start(Charge, Trap);
            OpenTab();

            Assert.That(CardOf(Charge).StateText.text, Is.EqualTo("Unlocked"));
            Assert.That(CardOf(Charge).DetailText.text, Is.EqualTo("Stamina | STR 10"));
            Assert.That(CardOf(Trap).StateText.text, Is.EqualTo("Missing Req"));
        }

        [Test]
        public void FirstEntryIsSelectedByDefault_AndSelectingACardFillsDetailsAndRequirementRows()
        {
            Start(Charge, Trap);
            OpenTab();
            Assert.That(CardOf(Charge).IsSelected, Is.True);
            Assert.That(View.DetailNameText.text, Is.EqualTo("Charge"));

            CardOf(Trap).Button.onClick.Invoke();

            Assert.That(CardOf(Trap).IsSelected, Is.True);
            Assert.That(CardOf(Charge).IsSelected, Is.False);
            Assert.That(View.DetailNameText.text, Is.EqualTo("Trap"));
            Assert.That(View.RequirementRows, Has.Count.EqualTo(1));
            Assert.That(View.RequirementRows[0].LabelText.text, Is.EqualTo("DEX (Trap)"));
            Assert.That(View.RequirementRows[0].ValueText.text, Is.EqualTo("3 / 10"));
            Assert.That(View.RequirementRows[0].StatusText.text, Is.EqualTo("X"));
        }

        [Test]
        public void EquipToSlot1_PersistsAndRefreshesBadgeSlotPanelAndButton()
        {
            Start(Charge, Trap);
            OpenTab();

            View.EquipButton(UniversalAbilitySlot.Slot1).onClick.Invoke();

            Assert.That(_store.GetPreparedAbilities().Slot1, Is.EqualTo(Charge));
            Assert.That(CardOf(Charge).StateText.text, Is.EqualTo("Equipped"));
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot1).NameText.text, Is.EqualTo("Charge"));
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot1).ClearButton.interactable, Is.True);
            Assert.That(View.EquipButton(UniversalAbilitySlot.Slot1).interactable, Is.False);
            Assert.That(View.EquipButtonText(UniversalAbilitySlot.Slot1), Is.EqualTo("Equipped in Slot 1"));
        }

        [Test]
        public void AnAbilityEquippedInOneSlot_IsNotOfferedForTheOtherSlot()
        {
            Start(Charge);
            OpenTab();
            View.EquipButton(UniversalAbilitySlot.Slot1).onClick.Invoke();

            Assert.That(View.EquipButton(UniversalAbilitySlot.Slot2).interactable, Is.False);
            Assert.That(View.EquipButtonText(UniversalAbilitySlot.Slot2), Is.EqualTo("Equip to Slot 2"));
        }

        [Test]
        public void AnAbilityWhoseRequirementsAreNotMet_CannotBeEquipped()
        {
            Start(Trap);
            OpenTab();

            Assert.That(View.EquipButton(UniversalAbilitySlot.Slot1).interactable, Is.False);
            Assert.That(View.EquipButton(UniversalAbilitySlot.Slot2).interactable, Is.False);
        }

        [Test]
        public void Clear_EmptiesTheSlot()
        {
            Start(Charge);
            OpenTab();
            View.EquipButton(UniversalAbilitySlot.Slot2).onClick.Invoke();
            Assert.That(_store.GetPreparedAbilities().Slot2, Is.EqualTo(Charge));

            View.SlotView(UniversalAbilitySlot.Slot2).ClearButton.onClick.Invoke();

            Assert.That(_store.GetPreparedAbilities().Slot2.IsValid, Is.False);
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot2).DetailText.text, Is.EqualTo("Empty"));
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot2).ClearButton.interactable, Is.False);
            Assert.That(CardOf(Charge).StateText.text, Is.EqualTo("Unlocked"));
        }

        [Test]
        public void WhenTheGateIsClosed_EquipAndClearAreDisabledAndTheLockedLineIsShown()
        {
            Start(Charge);
            OpenTab();
            View.EquipButton(UniversalAbilitySlot.Slot1).onClick.Invoke();
            Assert.That(View.LockedNote.activeSelf, Is.False);

            _canMutate = false;
            ReopenTab();

            Assert.That(View.LockedNote.activeSelf, Is.True);
            Assert.That(View.EquipButton(UniversalAbilitySlot.Slot2).interactable, Is.False);
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot1).ClearButton.interactable, Is.False);
        }

        [Test]
        public void ABlockedMutation_NeverReachesTheStore()
        {
            Start(Charge);
            OpenTab();
            _canMutate = false;

            View.EquipButton(UniversalAbilitySlot.Slot1).onClick.Invoke();

            Assert.That(_store.GetPreparedAbilities().Slot1.IsValid, Is.False);
        }

        [Test]
        public void EmptyRepertoire_ShowsASingleNoteAndNoCards()
        {
            Start();
            OpenTab();

            Assert.That(View.Cards, Is.Empty);
            Assert.That(View.ListNote.gameObject.activeSelf, Is.True);
            Assert.That(View.ListNote.text, Is.EqualTo("No abilities unlocked yet."));
            Assert.That(View.DetailsRoot.activeSelf, Is.False);
            Assert.That(View.DetailsNote.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void AProfileCommitWhileShown_RefreshesTheView()
        {
            Start(Charge);
            OpenTab();

            Assert.That(_store.TrySetPreparedAbility(UniversalAbilitySlot.Slot2, Charge), Is.EqualTo(AbilityPreparationResult.Success));

            Assert.That(CardOf(Charge).StateText.text, Is.EqualTo("Equipped"));
            Assert.That(View.SlotView(UniversalAbilitySlot.Slot2).NameText.text, Is.EqualTo("Charge"));
        }

        [Test]
        public void ReopeningTheTab_PicksUpChangesMadeWhileItWasHidden()
        {
            Start(Charge);
            OpenTab();
            OpenTab(); // toggles closed
            Assert.That(_menu.IsOpen, Is.False);

            Assert.That(_store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, Charge), Is.EqualTo(AbilityPreparationResult.Success));
            OpenTab();

            Assert.That(CardOf(Charge).StateText.text, Is.EqualTo("Equipped"));
        }

        [UnityTest]
        public IEnumerator Unregister_DestroysTheViewRemovesTheTabAndStopsListening()
        {
            Start(Charge);
            OpenTab();
            Assert.That(View, Is.Not.Null);

            _abilities.Unregister();
            yield return null; // Destroy is deferred to the end of the frame.

            Assert.That(_canvas.GetComponentInChildren<TownAbilitiesView>(true), Is.Null);
            Assert.That(_menu.IsOpen, Is.False, "Unregistering the shown tab closes the menu.");
            Assert.DoesNotThrow(() => _store.TrySetPreparedAbility(UniversalAbilitySlot.Slot1, Charge));
            OpenTab();
            Assert.That(_menu.IsOpen, Is.False, "The tab no longer exists.");
        }

        [Test]
        public void WithoutAStore_TheTabShowsAnUnavailableNote()
        {
            _context.Initialize(null, null, null, null, null);
            _abilities.Register(_menu, _context, () => true);

            OpenTab();

            Assert.That(View.UnavailableNote.activeSelf, Is.True);
            Assert.That(View.ContentRoot.activeSelf, Is.False);
        }

        private TownAbilitiesView View => _canvas.GetComponentInChildren<TownAbilitiesView>(true);

        private TownAbilityCardView CardOf(AbilityId id)
        {
            foreach (TownAbilityCardView card in View.Cards)
            {
                if (card.Id == id)
                {
                    return card;
                }
            }

            Assert.Fail($"No card for {id.Value}.");
            return null;
        }

        private List<AbilityId> CardIds()
        {
            var ids = new List<AbilityId>();
            foreach (TownAbilityCardView card in View.Cards)
            {
                ids.Add(card.Id);
            }

            return ids;
        }

        private void OpenTab() => InvokeMenu("TryOpenTab", TownMenuTabIds.Abilities);

        private void ReopenTab()
        {
            OpenTab();
            OpenTab();
        }

        private void InvokeMenu(string method, string tabId)
        {
            MethodInfo info = typeof(TownPlayerMenuPresenter).GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null);
            info.Invoke(_menu, new object[] { tabId });
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"{path} must exist.");
            return asset;
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} must declare {name}.");
            field.SetValue(target, value);
        }
    }
}
#endif
