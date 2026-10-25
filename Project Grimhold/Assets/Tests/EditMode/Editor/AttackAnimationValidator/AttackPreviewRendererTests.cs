#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewRendererTests
    {
        [Test]
        public void Render_ResizingAndFailedSeekPreserveActiveRenderTarget()
        {
            using var rig = new AttackPreviewRig();
            rig.Configure(new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3));
            using var renderer = new AttackPreviewRenderer(rig);
            var sentinel = new RenderTexture(8, 8, 0);
            var previous = RenderTexture.active;
            Texture2D first = null, resized = null;
            try
            {
                Assert.That(sentinel.Create(), Is.True);
                RenderTexture.active = sentinel;
                first = renderer.Render(0, 64, 64);
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                resized = renderer.Render(0.1f, 128, 128);
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => renderer.Render(-1, 256, 256));
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
            }
            finally
            {
                RenderTexture.active = previous;
                if (first != null) Object.DestroyImmediate(first);
                if (resized != null) Object.DestroyImmediate(resized);
                sentinel.Release();
                Object.DestroyImmediate(sentinel);
            }
        }

        [Test]
        public void RealRigFrame_IsNonBackgroundAndDeterministicAfterBackwardSeek()
        {
            using var rig = new AttackPreviewRig();
            rig.Configure(new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3));
            using var renderer = new AttackPreviewRenderer(rig);
            Texture2D first = null, other = null, repeat = null;
            var previous = RenderTexture.active;
            try
            {
                first = renderer.Render(rig.Duration * 0.5f, 128, 128);
                other = renderer.Render(rig.Duration + 0.1f, 128, 128);
                repeat = renderer.Render(rig.Duration * 0.5f, 128, 128);
                Assert.That(first.width, Is.EqualTo(128));
                Assert.That(first.height, Is.EqualTo(128));
                var pixels = first.GetPixels32();
                // Compare against a corner of the actual target to avoid color-space assumptions.
                var corner = pixels[0];
                Assert.That(pixels.Count(p => !p.Equals(corner)), Is.GreaterThan(20), "Rendering must include the actual avatar, not just the clear color.");
                Assert.That(repeat.GetPixels32(), Is.EqualTo(pixels));
                Assert.That(RenderTexture.active, Is.SameAs(previous));
            }
            finally
            {
                if (first != null) Object.DestroyImmediate(first);
                if (other != null) Object.DestroyImmediate(other);
                if (repeat != null) Object.DestroyImmediate(repeat);
            }
        }
    }
}
#endif
