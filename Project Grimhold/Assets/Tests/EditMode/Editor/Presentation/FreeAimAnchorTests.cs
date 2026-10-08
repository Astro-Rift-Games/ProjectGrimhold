using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class FreeAimAnchorTests
    {
        private const float Tolerance = 0.0001f;

        private static readonly Vector2[] Aims =
        {
            Vector2.right, Vector2.up, Vector2.left, Vector2.down,
            new Vector2(0.6f, 0.8f), new Vector2(-0.6f, 0.8f), new Vector2(-0.6f, -0.8f), new Vector2(0.6f, -0.8f)
        };

        [Test]
        public void Resolve_PlacesTheRadiusAlongTheAim()
        {
            foreach (Vector2 aim in Aims)
            {
                Vector2 anchor = FreeAimAnchor.Resolve(aim, new Vector2(0.6f, 0f));

                Assert.That(anchor.x, Is.EqualTo(aim.normalized.x * 0.6f).Within(Tolerance), aim.ToString());
                Assert.That(anchor.y, Is.EqualTo(aim.normalized.y * 0.6f).Within(Tolerance), aim.ToString());
            }
        }

        [Test]
        public void Resolve_LateralOffsetIsPerpendicularToTheAim()
        {
            Vector2 anchor = FreeAimAnchor.Resolve(Vector2.right, new Vector2(0f, 0.2f));

            Assert.That(anchor.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(anchor.y, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Resolve_LateralOffsetMirrorsWithTheAim()
        {
            Vector2 anchor = FreeAimAnchor.Resolve(Vector2.left, new Vector2(0f, 0.2f));

            // A left aim mirrors the weapon, so the same side of the body keeps the weapon: world y stays up.
            Assert.That(anchor.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(anchor.y, Is.EqualTo(0.2f).Within(Tolerance));
        }

        [Test]
        public void Resolve_KeepsTheDistanceFromTheBodyOriginForEveryAim()
        {
            Vector2 stance = new Vector2(0.5f, 0.12f);
            foreach (Vector2 aim in Aims)
            {
                Assert.That(FreeAimAnchor.Resolve(aim, stance).magnitude,
                    Is.EqualTo(stance.magnitude).Within(Tolerance), aim.ToString());
            }
        }

        [Test]
        public void Resolve_ZeroStanceStaysOnTheBodyOrigin()
        {
            Assert.That(FreeAimAnchor.Resolve(Vector2.up, Vector2.zero), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Resolve_NonUnitAimIsNormalized()
        {
            Vector2 anchor = FreeAimAnchor.Resolve(new Vector2(0f, 5f), new Vector2(0.4f, 0f));

            Assert.That(anchor.y, Is.EqualTo(0.4f).Within(Tolerance));
        }
    }
}
