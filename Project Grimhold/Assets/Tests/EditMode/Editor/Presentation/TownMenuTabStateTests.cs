#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class TownMenuTabStateTests
    {
        private const string Inventory = "inventory";
        private const string Attributes = "attributes";

        private static TownMenuTabState Create() => new(new[] { Inventory, Attributes });

        [Test]
        public void Construction_StartsClosedOnFirstTab()
        {
            var state = Create();

            Assert.That(state.IsOpen, Is.False);
            Assert.That(state.SelectedTabId, Is.EqualTo(Inventory));
        }

        [Test]
        public void Construction_RejectsEmptyDuplicateOrBlankTabIds()
        {
            Assert.Throws<ArgumentException>(() => new TownMenuTabState(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new TownMenuTabState(new[] { Inventory, Inventory }));
            Assert.Throws<ArgumentException>(() => new TownMenuTabState(new[] { Inventory, " " }));
            Assert.Throws<ArgumentNullException>(() => new TownMenuTabState(null));
        }

        [Test]
        public void Open_WhenClosed_OpensOnRequestedTab()
        {
            var state = Create();

            var result = state.Open(Attributes);

            Assert.That(result, Is.EqualTo(TownMenuTabTransition.Opened));
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.SelectedTabId, Is.EqualTo(Attributes));
        }

        [Test]
        public void Open_SameTabWhileOpen_ClosesAsHotkeyToggle()
        {
            var state = Create();
            state.Open(Inventory);

            var result = state.Open(Inventory);

            Assert.That(result, Is.EqualTo(TownMenuTabTransition.Closed));
            Assert.That(state.IsOpen, Is.False);
        }

        [Test]
        public void Open_OtherTabWhileOpen_SwitchesWithoutClosing()
        {
            var state = Create();
            state.Open(Inventory);

            var result = state.Open(Attributes);

            Assert.That(result, Is.EqualTo(TownMenuTabTransition.Switched));
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.SelectedTabId, Is.EqualTo(Attributes));
        }

        [Test]
        public void Open_UnknownTab_IsIgnoredAndKeepsState()
        {
            var state = Create();
            state.Open(Inventory);

            var result = state.Open("missing");

            Assert.That(result, Is.EqualTo(TownMenuTabTransition.Ignored));
            Assert.That(state.IsOpen, Is.True);
            Assert.That(state.SelectedTabId, Is.EqualTo(Inventory));
        }

        [Test]
        public void Close_WhenOpen_ClosesAndKeepsSelectedTab()
        {
            var state = Create();
            state.Open(Attributes);

            var closed = state.Close();

            Assert.That(closed, Is.True);
            Assert.That(state.IsOpen, Is.False);
            Assert.That(state.SelectedTabId, Is.EqualTo(Attributes));
        }

        [Test]
        public void Close_WhenAlreadyClosed_ReturnsFalse()
        {
            var state = Create();

            Assert.That(state.Close(), Is.False);
        }

        [Test]
        public void Select_WhileOpen_SwitchesTab()
        {
            var state = Create();
            state.Open(Inventory);

            var changed = state.Select(Attributes);

            Assert.That(changed, Is.True);
            Assert.That(state.SelectedTabId, Is.EqualTo(Attributes));
            Assert.That(state.IsOpen, Is.True);
        }

        [Test]
        public void Select_SameUnknownOrClosed_ReturnsFalse()
        {
            var state = Create();

            Assert.That(state.Select(Attributes), Is.False, "closed menu cannot switch tabs");

            state.Open(Inventory);

            Assert.That(state.Select(Inventory), Is.False, "already selected");
            Assert.That(state.Select("missing"), Is.False, "unknown tab");
            Assert.That(state.SelectedTabId, Is.EqualTo(Inventory));
        }

        [Test]
        public void Reopen_AfterClose_ViaOtherTabOpensThatTab()
        {
            var state = Create();
            state.Open(Inventory);
            state.Close();

            var result = state.Open(Attributes);

            Assert.That(result, Is.EqualTo(TownMenuTabTransition.Opened));
            Assert.That(state.SelectedTabId, Is.EqualTo(Attributes));
        }
    }
}
#endif
