#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class TownProgressionFeedbackTests
    {
        [Test]
        public void ConfirmedQueue_IsProfileScopedAndConsumedExactlyOnce()
        {
            ExperienceCurve.TryCreate(new long[] { 100, 200, 300 }, out var curve);
            CharacterProgressionRules.TryApplyExperience(curve, 1, 50, 300, out var result);
            var queue = new ConfirmedProgressionFeedbackQueue();
            var profile = new ProfileId("11111111111111111111111111111111");
            queue.Record(profile, result);
            Assert.That(queue.TryTake(new ProfileId("22222222222222222222222222222222"), out _), Is.False);
            Assert.That(queue.TryTake(profile, out var confirmed), Is.True);
            Assert.That(confirmed.ResultingLevel, Is.EqualTo(3));
            Assert.That(queue.TryTake(profile, out _), Is.False);
        }

        [Test]
        public void Animation_CrossesEveryThresholdAndEndsAtExactConfirmedRemainder()
        {
            ExperienceCurve.TryCreate(new long[] { 100, 200, 300 }, out var curve);
            CharacterProgressionRules.TryApplyExperience(curve, 1, 50, 300, out var result);
            Assert.That(TownProgressionFeedbackAnimation.TrySample(curve, result, 0, out var start), Is.True);
            Assert.That(start.Level, Is.EqualTo(1));
            Assert.That(start.CurrentExperience, Is.EqualTo(50));
            TownProgressionFeedbackAnimation.TrySample(curve, result, .5f, out var middle);
            Assert.That(middle.Level, Is.EqualTo(2));
            TownProgressionFeedbackAnimation.TrySample(curve, result, 1, out var end);
            Assert.That(end.Level, Is.EqualTo(3));
            Assert.That(end.CurrentExperience, Is.EqualTo(50));
        }

        [Test]
        public void Queue_DoesNotReplayNoOpOrOldLoginState()
        {
            var queue = new ConfirmedProgressionFeedbackQueue();
            Assert.That(queue.TryTake(new ProfileId("11111111111111111111111111111111"), out _), Is.False);
            queue.Record(default, default);
            Assert.That(queue.TryTake(default, out _), Is.False);
        }
    }
}
#endif
