#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class HitstopTargetFreezeTests
    {
        private GameObject _first, _second;
        private HitstopTargetFreeze _freeze;

        [SetUp]
        public void SetUp()
        {
            _first = new GameObject("Hitstop target");
            _second = new GameObject("Replacement target");
            _freeze = new HitstopTargetFreeze();
        }

        [TearDown]
        public void TearDown()
        {
            _freeze.Restore();
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
        }

        [Test]
        public void DelayHoldAndExpiry_RestoreOriginalSpeedsIncludingInactiveChildren()
        {
            Animator first = _first.AddComponent<Animator>();
            first.speed = .75f;
            _second.transform.SetParent(_first.transform);
            Animator child = _second.AddComponent<Animator>();
            child.speed = 1.5f;
            _second.SetActive(false);
            _freeze.Freeze(_first.transform, .1f, .1f);
            Assert.That(first.speed, Is.EqualTo(.75f));
            _freeze.Tick(.1f);
            Assert.That(first.speed, Is.Zero);
            Assert.That(child.speed, Is.Zero);
            _first.SetActive(false);
            _freeze.Tick(.1f);
            Assert.That(first.speed, Is.EqualTo(.75f));
            Assert.That(child.speed, Is.EqualTo(1.5f));
        }

        [Test]
        public void ReplacementAndExplicitRestore_NeverLeavePreviousTargetFrozen()
        {
            Animator first = _first.AddComponent<Animator>();
            Animator second = _second.AddComponent<Animator>();
            first.speed = .5f;
            second.speed = 2f;
            _freeze.Freeze(_first.transform, 0f, .1f);
            Assert.That(first.speed, Is.Zero);
            _freeze.Freeze(_second.transform, 0f, .1f);
            Assert.That(first.speed, Is.EqualTo(.5f));
            Assert.That(second.speed, Is.Zero);
            _freeze.Restore();
            _freeze.Restore();
            Assert.That(second.speed, Is.EqualTo(2f));
        }

        [Test]
        public void FrameCrossingEntireWindow_LeavesNoFrozenAnimator()
        {
            Animator animator = _first.AddComponent<Animator>();
            animator.speed = 1.25f;
            _freeze.Freeze(_first.transform, .02f, .04f);
            _freeze.Tick(.2f);
            Assert.That(animator.speed, Is.EqualTo(1.25f));
        }

        [Test]
        public void DestroyedAnimators_AreSkippedOnRestore()
        {
            Animator animator = _first.AddComponent<Animator>();
            _freeze.Freeze(_first.transform, 0f, .1f);
            Object.DestroyImmediate(animator);
            Assert.DoesNotThrow(() => _freeze.Restore());
        }
    }
}
#endif
