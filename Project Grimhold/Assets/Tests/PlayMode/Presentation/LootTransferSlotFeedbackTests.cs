#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Presentation
{
    public sealed class LootTransferSlotFeedbackTests
    {
        private GameObject _root;
        private RaidInventorySlotView _slot;
        private Image _background;
        private static readonly LootId Bone = new("bone");

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TransferFeedbackSlot", typeof(RectTransform), typeof(Image));
            _background = _root.GetComponent<Image>();
            _slot = _root.AddComponent<RaidInventorySlotView>();
            typeof(RaidInventorySlotView).GetField("_background", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_slot, _background);
            _slot.Present(RaidInventorySlotData.Create(new LootEntry(Bone, 1), null, null));
            _slot.SetInteraction(true, true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [UnityTest]
        public IEnumerator Pulse_RestoresSelectedColorAfterDuration()
        {
            Color selected = _background.color;
            _slot.ShowTransferSuccess();
            Assert.That(_slot.IsTransferFeedbackActive, Is.True);
            Assert.That(_background.color, Is.Not.EqualTo(selected));
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_slot.IsTransferFeedbackActive, Is.False);
            Assert.That(_background.color, Is.EqualTo(selected));
        }

        [TestCase(DropHighlightState.Valid)]
        [TestCase(DropHighlightState.Invalid)]
        public void Pulse_RestoresCurrentDropHighlight(DropHighlightState highlight)
        {
            _slot.SetDropHighlight(highlight);
            Color expected = _background.color;
            _slot.ShowTransferSuccess();
            _slot.SetInteraction(true, false);
            _slot.ClearTransferFeedback();
            Assert.That(_background.color, Is.EqualTo(expected));
        }

        [Test]
        public void ClearReplacementAndDisable_DiscardPulse()
        {
            _slot.ShowTransferSuccess();
            _slot.Present(RaidInventorySlotData.Create(new LootEntry(new LootId("coins"), 1), null, null));
            Assert.That(_slot.IsTransferFeedbackActive, Is.False);
            _slot.ShowTransferSuccess();
            _root.SetActive(false);
            Assert.That(_slot.IsTransferFeedbackActive, Is.False);
            _root.SetActive(true);
            _slot.ShowTransferSuccess();
            _slot.Clear();
            Assert.That(_slot.IsTransferFeedbackActive, Is.False);
        }

        [Test]
        public void RefreshSameItem_PreservesPulseAndLatestSelectedState()
        {
            _slot.ShowTransferSuccess();
            _slot.Present(RaidInventorySlotData.Create(new LootEntry(Bone, 2), null, null));
            _slot.SetInteraction(false, false);
            Assert.That(_slot.IsTransferFeedbackActive, Is.True);
            _slot.ClearTransferFeedback();
            Assert.That(_background.color, Is.EqualTo(new Color(0.12f, 0.12f, 0.14f, 0.95f)));
        }
    }
}
#endif
