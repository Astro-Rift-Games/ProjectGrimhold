using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidInventoryDefeatPolicyTests
    {
        [Test]
        public void DefeatedMutationBlock_ClosesOpenInventoryAndIsReversible()
        {
            var owner = new GameObject("RaidInventoryDefeatPolicyTests");
            try
            {
                RaidInventoryPresenter presenter = owner.AddComponent<RaidInventoryPresenter>();
                FieldInfo modeField = typeof(RaidInventoryPresenter).GetField(
                    "_mode",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(modeField, Is.Not.Null);
                modeField.SetValue(presenter, Enum.Parse(modeField.FieldType, "Personal"));
                Assert.That(presenter.IsOpen, Is.True);

                presenter.SetGameplayMutationsBlocked(true);

                Assert.That(presenter.GameplayMutationsBlocked, Is.True);
                Assert.That(presenter.IsOpen, Is.False);

                presenter.SetGameplayMutationsBlocked(false);
                Assert.That(presenter.GameplayMutationsBlocked, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
