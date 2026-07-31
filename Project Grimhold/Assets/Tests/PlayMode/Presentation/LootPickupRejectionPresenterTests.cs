#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    public sealed class LootPickupRejectionPresenterTests
    {
        [UnityTest]
        public IEnumerator RejectionAnimatesOnlyVisualAndRestoresItsLocalPose()
        {
            var root = new GameObject("PickupRoot");
            var visual = new GameObject("WorldVisual").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0.1f, -0.2f, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, 17f);
            Vector3 rootPosition = new(4f, 5f, 0f);
            Quaternion rootRotation = Quaternion.Euler(0f, 0f, 23f);
            root.transform.SetPositionAndRotation(rootPosition, rootRotation);

            LootPickupRejectionPresenter presenter =
                root.AddComponent<LootPickupRejectionPresenter>();
            SetField(presenter, "_visualTransform", visual);
            SetField(presenter, "_duration", 0.03f);

            Vector3 localPosition = visual.localPosition;
            Quaternion localRotation = visual.localRotation;
            presenter.PlayRejectedPickup();

            Assert.That(root.transform.position, Is.EqualTo(rootPosition));
            Assert.That(root.transform.rotation, Is.EqualTo(rootRotation));

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(root.transform.position, Is.EqualTo(rootPosition));
            Assert.That(root.transform.rotation, Is.EqualTo(rootRotation));
            Assert.That(visual.localPosition, Is.EqualTo(localPosition));
            Assert.That(visual.localRotation, Is.EqualTo(localRotation));

            Object.Destroy(root);
        }

        private static void SetField(object owner, string fieldName, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(owner, value);
        }
    }
}
#endif
