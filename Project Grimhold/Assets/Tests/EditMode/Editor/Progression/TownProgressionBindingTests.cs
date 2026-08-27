#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class TownProgressionBindingTests
    {
        private readonly ProfileId _observedProfile =
            new("11111111111111111111111111111111");
        private readonly ProfileId _otherProfile =
            new("22222222222222222222222222222222");
        private ExperienceCurve _curve;
        private int _level;
        private long _experience;
        private int _subscribeCount;
        private int _unsubscribeCount;
        private int _readCount;
        private int _unavailableCount;
        private readonly List<TownProgressionPresentation> _presentations = new();
        private readonly List<string> _operationOrder = new();
        private Action<ProfileId> _activeHandler;
        private Action<ProfileId> _lastSubscribedHandler;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ExperienceCurve.TryCreate(
                new long[] { 100, 200 },
                out _curve), Is.True);
            _level = 1;
            _experience = 0;
            _subscribeCount = 0;
            _unsubscribeCount = 0;
            _readCount = 0;
            _unavailableCount = 0;
            _presentations.Clear();
            _operationOrder.Clear();
            _activeHandler = null;
            _lastSubscribedHandler = null;
        }

        [Test]
        public void Construction_SubscribesOnceBeforeImmediateInitialRead()
        {
            using var binding = CreateBinding();

            Assert.That(_subscribeCount, Is.EqualTo(1));
            Assert.That(_readCount, Is.EqualTo(1));
            Assert.That(_operationOrder, Is.EqualTo(new[] { "subscribe", "read" }));
            Assert.That(_presentations, Has.Count.EqualTo(1));
            Assert.That(_presentations[0].Level, Is.EqualTo(1));
        }

        [Test]
        public void ObservedProfileCommit_RefreshesCurrentState()
        {
            using var binding = CreateBinding();
            _level = 2;
            _experience = 50;

            Publish(_observedProfile);

            Assert.That(_readCount, Is.EqualTo(2));
            Assert.That(_presentations, Has.Count.EqualTo(2));
            Assert.That(_presentations[1].Level, Is.EqualTo(2));
            Assert.That(_presentations[1].CurrentExperience, Is.EqualTo(50));
        }

        [Test]
        public void OtherProfileCommit_IsIgnoredWithoutReading()
        {
            using var binding = CreateBinding();

            Publish(_otherProfile);

            Assert.That(_readCount, Is.EqualTo(1));
            Assert.That(_presentations, Has.Count.EqualTo(1));
            Assert.That(_unavailableCount, Is.Zero);
        }

        [Test]
        public void ValidInvalidValid_CommunicatesUnavailableAndRecoversWithoutStalePresentation()
        {
            using var binding = CreateBinding();
            _experience = 100;

            Publish(_observedProfile);

            Assert.That(_presentations, Has.Count.EqualTo(1));
            Assert.That(_unavailableCount, Is.EqualTo(1));

            _level = 2;
            _experience = 25;
            Publish(_observedProfile);

            Assert.That(_presentations, Has.Count.EqualTo(2));
            Assert.That(_presentations[1].Level, Is.EqualTo(2));
            Assert.That(_presentations[1].CurrentExperience, Is.EqualTo(25));
            Assert.That(_unavailableCount, Is.EqualTo(1));
        }

        [Test]
        public void InitialInvalidState_ReportsUnavailableWithoutPresentation()
        {
            _experience = -1;

            using var binding = CreateBinding();

            Assert.That(_presentations, Is.Empty);
            Assert.That(_unavailableCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_IsIdempotentAndPreventsCallbacks()
        {
            TownProgressionBinding binding = CreateBinding();
            Action<ProfileId> subscribedHandler = _lastSubscribedHandler;

            binding.Dispose();
            binding.Dispose();
            subscribedHandler(_observedProfile);

            Assert.That(_unsubscribeCount, Is.EqualTo(1));
            Assert.That(_readCount, Is.EqualTo(1));
            Assert.That(_presentations, Has.Count.EqualTo(1));
            Assert.That(_unavailableCount, Is.Zero);
        }

        private TownProgressionBinding CreateBinding() => new(
            _observedProfile,
            _curve,
            ReadState,
            Subscribe,
            Unsubscribe,
            presentation => _presentations.Add(presentation),
            () => _unavailableCount++);

        private (int Level, long Experience) ReadState()
        {
            _operationOrder.Add("read");
            _readCount++;
            return (_level, _experience);
        }

        private void Subscribe(Action<ProfileId> handler)
        {
            _operationOrder.Add("subscribe");
            _subscribeCount++;
            _activeHandler += handler;
            _lastSubscribedHandler = handler;
        }

        private void Unsubscribe(Action<ProfileId> handler)
        {
            _unsubscribeCount++;
            _activeHandler -= handler;
        }

        private void Publish(ProfileId profileId)
        {
            _activeHandler?.Invoke(profileId);
        }
    }
}
#endif
