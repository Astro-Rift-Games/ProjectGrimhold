#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownInventoryPresenterPlayModeTests
    {
        private const string SharedPrefabPath = "Assets/Prefabs/UI/RaidInventoryUI.prefab";

        private GameObject _instance;
        private GameObject _readerObject;
        private RaidInventoryPresenter _presenter;
        private RaidInventoryView _view;
        private PlayerInputReader _reader;
        private FakeInventoryReadSource _source;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            _instance = Object.Instantiate(prefab);
            _presenter = _instance.GetComponent<RaidInventoryPresenter>();
            _view = _instance.GetComponent<RaidInventoryView>();
            _readerObject = new GameObject("TownInventoryInputReaderTests");
            _reader = _readerObject.AddComponent<PlayerInputReader>();
            _source = new FakeInventoryReadSource();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter?.Unbind();
            Object.DestroyImmediate(_instance);
            Object.DestroyImmediate(_readerObject);
        }

        [Test]
        public void TownBinding_TogglesScreenAndCleansSubscriptionsAndSuppression()
        {
            _presenter.BindTown(_source, _source, _source, _reader);
            Assert.That(_source.SubscriberCount, Is.EqualTo(1));
            Assert.That(_presenter.IsOpen, Is.False);

            InvokePresenter("OnInventoryToggleRequested");
            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);
            Assert.That(_view.ContainerPanel.gameObject.activeSelf, Is.False);
            Assert.That(FindDescendant(_instance.transform, "EquipmentPanel").gameObject.activeSelf, Is.True);

            _source.PublishChange();
            Assert.That(_presenter.IsOpen, Is.True, "A persistent commit must refresh without reopening the screen.");

            InvokePresenter("OnInventoryToggleRequested");
            Assert.That(_presenter.IsOpen, Is.False);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);

            _presenter.Unbind();
            _presenter.Unbind();
            Assert.That(_source.SubscriberCount, Is.Zero);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.False);
        }

        [Test]
        public void TownBinding_ProjectsEightPreparedSlotsAndAppliesReadyGate()
        {
            _source.PreparedEquipment = new PreparedEquipmentLoadout(
                new LootId("training_sword"),
                new LootId("recovery_sword"),
                new LootId("light_armor_open_sallet"),
                new LootId("light_armor_chain_mail_armor"),
                new LootId("light_armor_gloves"),
                new LootId("light_armor_chain_mail_trousers"),
                weaponSetAOffHand: new LootId("wand"),
                weaponSetBOffHand: new LootId("spellbook"));
            _presenter.BindTown(_source, _source, _source, _reader);
            InvokePresenter("OnInventoryToggleRequested");

            AssertEquipmentSlot("WeaponSetAMainHand", _source.PreparedEquipment.WeaponSetAMainHand, true);
            AssertEquipmentSlot("WeaponSetAOffHand", _source.PreparedEquipment.WeaponSetAOffHand, true);
            AssertEquipmentSlot("WeaponSetBMainHand", _source.PreparedEquipment.WeaponSetBMainHand, true);
            AssertEquipmentSlot("WeaponSetBOffHand", _source.PreparedEquipment.WeaponSetBOffHand, true);
            AssertEquipmentSlot("Helmet", _source.PreparedEquipment.Helmet, true);
            AssertEquipmentSlot("Armor", _source.PreparedEquipment.Armor, true);
            AssertEquipmentSlot("Gloves", _source.PreparedEquipment.Gloves, true);
            AssertEquipmentSlot("Boots", _source.PreparedEquipment.Boots, true);

            _source.CanMutate = false;
            InvokePresenter("Update");
            AssertEquipmentSlot("WeaponSetAMainHand", _source.PreparedEquipment.WeaponSetAMainHand, false);
            InvokePresenter("OnEquipmentUnequipRequested", EquipmentSlot.WeaponSetAMainHand);
            Assert.That(_source.UnequipCalls, Is.Zero);

            _source.CanMutate = true;
            InvokePresenter("Update");
            InvokePresenter("OnEquipmentUnequipRequested", EquipmentSlot.WeaponSetAMainHand);
            Assert.That(_source.UnequipCalls, Is.EqualTo(1));
        }

        [Test]
        public void TownBinding_EquipmentContextUsesExactEquipmentSlotAnchor()
        {
            _source.PreparedEquipment = new PreparedEquipmentLoadout(
                default,
                default,
                new LootId("placeholder_helmet"));
            _presenter.BindTown(_source, _source, _source, _reader);
            InvokePresenter("OnInventoryToggleRequested");
            RectTransform helmet = (RectTransform)FindDescendant(_instance.transform, "Helmet");

            InvokePresenter("OnEquipmentSlotContextRequested", EquipmentSlot.Helmet, helmet);

            Assert.That(_view.ContextMenu.IsOpen, Is.True);
            Assert.That(_view.ContextMenu.CurrentAnchor, Is.SameAs(helmet));
        }

        private void InvokePresenter(string methodName)
        {
            InvokePresenter(methodName, null);
        }

        private void InvokePresenter(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, arguments);
        }

        private void AssertEquipmentSlot(string name, LootId expected, bool interactable)
        {
            RaidInventorySlotView slot = FindDescendant(_instance.transform, name)
                .GetComponent<RaidInventorySlotView>();
            Assert.That(slot, Is.Not.Null, name);
            Assert.That(slot.IsOccupied, Is.True, name);
            Assert.That(slot.LootId, Is.EqualTo(expected), name);
            Assert.That(slot.GetComponent<UnityEngine.UI.Button>().interactable, Is.EqualTo(interactable), name);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindDescendant(root.GetChild(index), name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private sealed class FakeInventoryReadSource :
            IInventoryReadSource,
            IPreparedEquipmentReadSource,
            ITownEquipmentMutationEndpoint
        {
            private Action _changed;
            public int SlotCapacity => LocalProfileSnapshot.MaxLoadoutSlots;
            public int Revision { get; private set; }
            public int SubscriberCount { get; private set; }
            public bool CanMutate { get; set; } = true;
            public PreparedEquipmentLoadout PreparedEquipment { get; set; }
            public int UnequipCalls { get; private set; }

            public event Action Changed
            {
                add
                {
                    _changed += value;
                    SubscriberCount++;
                }
                remove
                {
                    _changed -= value;
                    SubscriberCount--;
                }
            }

            public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
            {
                content = Array.Empty<LootEntry>();
                return true;
            }

            public bool TryGetPreparedEquipment(out PreparedEquipmentLoadout equipment)
            {
                equipment = PreparedEquipment;
                return true;
            }

            public bool CanEquip(LootId lootId, EquipmentSlot slot) => CanMutate;
            public StashOperationResult TryEquip(LootId lootId, EquipmentSlot slot) =>
                CanMutate ? StashOperationResult.Success : StashOperationResult.InvalidInventory;
            public StashOperationResult TryUnequip(EquipmentSlot slot)
            {
                if (!CanMutate)
                {
                    return StashOperationResult.InvalidInventory;
                }

                UnequipCalls++;
                return StashOperationResult.Success;
            }

            public void PublishChange()
            {
                Revision++;
                _changed?.Invoke();
            }
        }
    }
}
#endif
