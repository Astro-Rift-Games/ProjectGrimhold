#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ConsumableDurationConfigurationTests
{
    [Test]
    public void DefaultDuration_IsTwoSeconds_AndUnspawnedControllerIsReadable()
    {
        var definition = ScriptableObject.CreateInstance<ConsumableDefinition>();
        var owner = new GameObject("UnspawnedConsumableRead");
        try
        {
            Assert.That(definition.ConsumptionDurationSeconds, Is.EqualTo(2f));
            var controller = owner.AddComponent<PlayerConsumableNetworkController>();
            Assert.That(controller.IsConsuming, Is.False);
            Assert.That(controller.TryGetConsumingDefinition(out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(definition);
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidDuration_IsRejected(float duration)
    {
        var definition = ScriptableObject.CreateInstance<ConsumableDefinition>();
        try
        {
            typeof(ConsumableDefinition).GetField("_consumptionDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(definition, duration);
            Assert.That(definition.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("duration"));
        }
        finally { Object.DestroyImmediate(definition); }
    }
}
#endif
