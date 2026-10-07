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
    public sealed class LootTransferPresenterFeedbackTests
    {
        private GameObject _instance;
        private RaidInventoryPresenter _presenter;
        private RaidInventoryView _view;
        private LootTransferFeedbackState _feedback;
        private SnapshotSource _source;
        private static readonly LootId Bone = new("bone");

        [SetUp]
        public void SetUp()
        {
            _instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab"));
            _presenter = _instance.GetComponentInChildren<RaidInventoryPresenter>(true);
            _view = _instance.GetComponentInChildren<RaidInventoryView>(true);
            _source = new SnapshotSource();
            _feedback = Read<LootTransferFeedbackState>("_playerTransferFeedback");
            Write("_inventorySource", _source);
            Write("_isBound", true);
            Write("_mode", Enum.ToObject(Field("_mode").FieldType, 2));
            Invoke("RefreshPlayerPanel");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_instance);

        [TestCase(false)]
        [TestCase(true)]
        public void DestinationProjection_ConfirmationAndSnapshotEitherOrder_PulseOnlyUpdatedSlot(bool snapshotFirst)
        {
            _feedback.CaptureRequest(Bone);
            if (snapshotFirst)
            {
                _source.Entries = new[] { new LootEntry(Bone, 2) };
                Invoke("RefreshPlayerPanel");
                Assert.That(Slot(Bone).IsTransferFeedbackActive, Is.False);
            }
            _feedback.CompleteRequest(Bone, true);
            Invoke("RefreshPlayerPanel");
            if (!snapshotFirst)
            {
                Assert.That(AnyPulse(), Is.False);
                _source.Entries = new[] { new LootEntry(Bone, 2) };
                Invoke("RefreshPlayerPanel");
            }
            Assert.That(Slot(Bone).IsTransferFeedbackActive, Is.True);
        }

        [Test]
        public void UnrelatedConfirmation_AndClose_DoNotReplayPendingSuccess()
        {
            _feedback.CaptureRequest(Bone);
            var rejected = new LootTransferConfirmation(1, new EntityId(81), new EntityId(82), 0, 1,
                LootTransferResult.Rejected(LootTransferFailureReason.OutOfRange), Bone);
            Invoke("OnTransferConfirmed", rejected);
            Assert.That(AnyPulse(), Is.False);
            _feedback.CompleteRequest(Bone, true);
            _presenter.Close();
            _source.Entries = new[] { new LootEntry(Bone, 2) };
            Invoke("RefreshPlayerPanel");
            Assert.That(AnyPulse(), Is.False);
        }

        [Test]
        public void SuccessSound_IsOptionalAndRateLimitedWithoutDelayingVisuals()
        {
            Invoke("PlayTransferSound", new object[] { null });
            Assert.That(Read<float>("_nextTransferSoundTime"), Is.Zero);
            Write("_nextTransferSoundTime", Time.unscaledTime + 10f);
            _feedback.CaptureRequest(Bone);
            _feedback.CompleteRequest(Bone, true);
            _source.Entries = new[] { new LootEntry(Bone, 1) };
            Invoke("RefreshPlayerPanel");
            Assert.That(Slot(Bone).IsTransferFeedbackActive, Is.True);
            Assert.That(Read<float>("_nextTransferSoundTime"), Is.GreaterThan(Time.unscaledTime));
        }

        private RaidInventorySlotView Slot(LootId id)
        {
            foreach (var slot in _view.PlayerPanel.GetComponentsInChildren<RaidInventorySlotView>(true))
                if (slot.IsOccupied && slot.LootId == id) return slot;
            Assert.Fail("Destination LootId is not present in the refreshed panel.");
            return null;
        }

        private bool AnyPulse()
        {
            foreach (var slot in _view.PlayerPanel.GetComponentsInChildren<RaidInventorySlotView>(true))
                if (slot.IsTransferFeedbackActive) return true;
            return false;
        }

        private static FieldInfo Field(string name) => typeof(RaidInventoryPresenter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private T Read<T>(string name) => (T)Field(name).GetValue(_presenter);
        private void Write(string name, object value) => Field(name).SetValue(_presenter, value);
        private void Invoke(string name, params object[] args) => typeof(RaidInventoryPresenter)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, args);

        private sealed class SnapshotSource : IInventoryReadSource
        {
            public IReadOnlyList<LootEntry> Entries = Array.Empty<LootEntry>();
            public int SlotCapacity => 30;
            public int Revision => 0;
            public event Action Changed { add { } remove { } }
            public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
            {
                content = Entries;
                return true;
            }
        }
    }
}
#endif
