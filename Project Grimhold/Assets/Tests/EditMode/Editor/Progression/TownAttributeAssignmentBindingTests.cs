#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class TownAttributeAssignmentBindingTests
    {
        private readonly ProfileId _observed = new("81818181818181818181818181818181");
        private readonly ProfileId _other = new("82828282828282828282828282828282");
        private CharacterAttributeState _state;
        private bool _available;
        private int _subscribeCount;
        private int _unsubscribeCount;
        private int _readCount;
        private int _unavailableCount;
        private Action<ProfileId> _handler;
        private Action<ProfileId> _lastHandler;
        private readonly List<string> _order = new();
        private readonly List<TownAttributeAssignmentPresentation> _presentations = new();

        [SetUp]
        public void SetUp()
        {
            _state = ProgressionBalanceDefaults.InitialCharacterAttributeState;
            _available = true;
            _subscribeCount = 0;
            _unsubscribeCount = 0;
            _readCount = 0;
            _unavailableCount = 0;
            _handler = null;
            _lastHandler = null;
            _order.Clear();
            _presentations.Clear();
        }

        [Test]
        public void Construction_SubscribesOnceBeforeInitialTryRead()
        {
            using var binding = CreateBinding();

            Assert.That(_subscribeCount, Is.EqualTo(1));
            Assert.That(_readCount, Is.EqualTo(1));
            Assert.That(_order, Is.EqualTo(new[] { "subscribe", "read" }));
            Assert.That(_presentations, Has.Count.EqualTo(1));
        }

        [Test]
        public void ObservedCommitRefreshesAndOtherProfileIsIgnored()
        {
            using var binding = CreateBinding();
            Assert.That(CharacterAttributeState.TryCreate(
                6, 5, 5, 5, 5, 5, 9, out _state), Is.True);

            Publish(_other);
            Assert.That(_readCount, Is.EqualTo(1));

            Publish(_observed);
            Assert.That(_readCount, Is.EqualTo(2));
            Assert.That(_presentations, Has.Count.EqualTo(2));
            Assert.That(_presentations[1].Vitality, Is.EqualTo(6));
            Assert.That(_presentations[1].AvailablePoints, Is.EqualTo(9));
        }

        [Test]
        public void UnavailableReadClearsInsteadOfReusingPreviousProjection()
        {
            using var binding = CreateBinding();
            _available = false;

            Publish(_observed);

            Assert.That(_presentations, Has.Count.EqualTo(1));
            Assert.That(_unavailableCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_IsIdempotentAndPreventsCallbacks()
        {
            TownAttributeAssignmentBinding binding = CreateBinding();
            Action<ProfileId> subscribed = _lastHandler;

            binding.Dispose();
            binding.Dispose();
            subscribed(_observed);

            Assert.That(_unsubscribeCount, Is.EqualTo(1));
            Assert.That(_readCount, Is.EqualTo(1));
        }

        private TownAttributeAssignmentBinding CreateBinding() => new(
            _observed,
            ProgressionBalanceDefaults.InitialMaximumAttributeValue,
            TryRead,
            Subscribe,
            Unsubscribe,
            presentation => _presentations.Add(presentation),
            () => _unavailableCount++);

        private bool TryRead(out CharacterAttributeState state)
        {
            _order.Add("read");
            _readCount++;
            state = _available ? _state : default;
            return _available;
        }

        private void Subscribe(Action<ProfileId> handler)
        {
            _order.Add("subscribe");
            _subscribeCount++;
            _handler += handler;
            _lastHandler = handler;
        }

        private void Unsubscribe(Action<ProfileId> handler)
        {
            _unsubscribeCount++;
            _handler -= handler;
        }

        private void Publish(ProfileId profileId) => _handler?.Invoke(profileId);
    }
}
#endif
