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
        public void TownBinding_TogglesReadOnlyScreenAndCleansSubscriptionsAndSuppression()
        {
            _presenter.BindTown(_source, _reader);
            Assert.That(_source.SubscriberCount, Is.EqualTo(1));
            Assert.That(_presenter.IsOpen, Is.False);

            InvokePresenter("OnInventoryToggleRequested");
            Assert.That(_presenter.IsOpen, Is.True);
            Assert.That(_view.IsOpen, Is.True);
            Assert.That(_reader.IsGameplayInputSuppressed, Is.True);
            Assert.That(_view.ContainerPanel.gameObject.activeSelf, Is.False);
            Assert.That(FindDescendant(_instance.transform, "EquipmentPanel").gameObject.activeSelf, Is.False);

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

        private void InvokePresenter(string methodName)
        {
            MethodInfo method = typeof(RaidInventoryPresenter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, null);
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

        private sealed class FakeInventoryReadSource : IInventoryReadSource
        {
            private Action _changed;
            public int SlotCapacity => LocalProfileSnapshot.MaxLoadoutSlots;
            public int Revision { get; private set; }
            public int SubscriberCount { get; private set; }

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

            public void PublishChange()
            {
                Revision++;
                _changed?.Invoke();
            }
        }
    }
}
#endif
