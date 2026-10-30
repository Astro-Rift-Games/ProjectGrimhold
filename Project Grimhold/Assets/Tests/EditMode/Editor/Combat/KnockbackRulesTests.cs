#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class KnockbackRulesTests
{
    private KnockbackCurveConfig _config;

    [SetUp]
    public void SetUp() => _config = ScriptableObject.CreateInstance<KnockbackCurveConfig>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_config);

    [TestCase(40, 60, 0, KnockbackDecision.Absorbed, TestName = "Doc18_T01_ResistanceAbsorbs")]
    [TestCase(60, 60, 0, KnockbackDecision.Absorbed, TestName = "Doc18_T02_EqualResistanceAbsorbs")]
    [TestCase(80, 60, 20, KnockbackDecision.Started, TestName = "Doc18_T03_EffectivePowerStarts")]
    public void EffectivePower_Resolves(float power, float resistance, float expected, KnockbackDecision decision)
    {
        float effective = KnockbackRules.ComputeEffectivePower(power, resistance);
        Assert.That(effective, Is.EqualTo(expected));
        Assert.That(KnockbackRules.Resolve(true, effective, false, 0), Is.EqualTo(decision));
        if (effective > 0)
        {
            Assert.That(_config.TryResolveProfile(effective, 0.02f, out float distance, out _), Is.True);
            Assert.That(distance, Is.EqualTo(effective * 0.1f).Within(0.00001f));
        }
    }

    [TestCase(90, 20, KnockbackDecision.KeptActive, TestName = "Doc18_T04_WeakerKeepsActive")]
    [TestCase(20, 90, KnockbackDecision.Replaced, TestName = "Doc18_T05_StrongerReplaces")]
    [TestCase(20, 20, KnockbackDecision.KeptActive, TestName = "Doc18_T06_TieKeepsActive")]
    public void Replacement_IsStrict(float active, float incoming, KnockbackDecision expected)
        => Assert.That(KnockbackRules.Resolve(true, incoming, true, active), Is.EqualTo(expected));

    [Test]
    public void Doc18_T07_T11_EligibleHitMayDownTarget_TravelContinues()
    {
        Assert.That(KnockbackRules.Resolve(true, 20, false, 0), Is.EqualTo(KnockbackDecision.Started));
        Assert.That(KnockbackRules.ShouldContinue(true), Is.True);
    }

    [TestCase(false, TestName = "Doc18_T08_T13_AlreadyDownedWithoutActiveRejects")]
    [TestCase(true, TestName = "Doc18_T08_T12_AlreadyDownedRejectsStrongerReplacement")]
    public void DownedAtImpact_Rejects(bool hasActive)
        => Assert.That(KnockbackRules.Resolve(false, 90, hasActive, 20), Is.EqualTo(KnockbackDecision.RejectedIneligible));

    [Test]
    public void Doc18_T10_AbsorptionDoesNotDependOnDamage()
        => Assert.That(KnockbackRules.Resolve(true, 0, false, 0), Is.EqualTo(KnockbackDecision.Absorbed));

    [Test]
    public void Doc18_T14_DefeatStopsTravel() => Assert.That(KnockbackRules.ShouldContinue(false), Is.False);

    [TestCase(float.NaN, 5, 0)]
    [TestCase(float.PositiveInfinity, 5, 0)]
    [TestCase(-1, 5, 0)]
    [TestCase(10, -1, 10)]
    [TestCase(10, float.NaN, 10)]
    [TestCase(10, float.NegativeInfinity, 10)]
    public void InvalidInputs_AreTreatedAsZero(float power, float resistance, float expected)
        => Assert.That(KnockbackRules.ComputeEffectivePower(power, resistance), Is.EqualTo(expected));

    [TestCase(false)]
    [TestCase(true)]
    public void Steps_SumToDistance_AndReplacementStartsFresh(bool linear)
    {
        if (linear)
        {
            var serialized = new SerializedObject(_config);
            serialized.FindProperty("_displacementProfile").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Vector2 direction = new Vector2(0.6f, 0.8f);
        Assert.That((SumSteps(direction, 2, 17) - direction * 2).magnitude, Is.LessThan(0.00001f));
        // A replacement has an independent tick sequence and direction; no remaining old travel is added.
        Assert.That((SumSteps(Vector2.left, 3, 9) - Vector2.left * 3).magnitude, Is.LessThan(0.00001f));
    }

    private Vector2 SumSteps(Vector2 direction, float distance, int ticks)
    {
        Vector2 sum = Vector2.zero;
        for (int k = 1; k <= ticks; k++)
        {
            sum += KnockbackRules.ComputeStep(direction, distance,
                _config.EvaluateProgress((float)(k - 1) / ticks), _config.EvaluateProgress((float)k / ticks));
        }
        return sum;
    }

    [TestCase(0, 0.02f)]
    [TestCase(float.NaN, 0.02f)]
    [TestCase(20, 0)]
    [TestCase(20, float.PositiveInfinity)]
    public void InvalidProfileInputs_Fail(float power, float dt)
        => Assert.That(_config.TryResolveProfile(power, dt, out _, out _), Is.False);

    [Test]
    public void Profile_ClampsDistance_QuantizesDuration_AndForcesEndpoints()
    {
        Assert.That(_config.TryValidate(out string error), Is.True, error);
        Assert.That(_config.TryResolveProfile(100, 0.02f, out float distance, out int ticks), Is.True);
        Assert.That(distance, Is.EqualTo(3));
        Assert.That(ticks, Is.EqualTo(25));
        Assert.That(_config.TryResolveProfile(1, 1, out _, out ticks), Is.True);
        Assert.That(ticks, Is.EqualTo(1));
        Assert.That(_config.EvaluateProgress(-1), Is.Zero);
        Assert.That(_config.EvaluateProgress(2), Is.EqualTo(1));
        Assert.That(KnockbackRules.IsComplete(24, 25), Is.False);
        Assert.That(KnockbackRules.IsComplete(25, 25), Is.True);
    }

    [Test]
    public void InvalidConfiguration_IsRejected()
    {
        var serialized = new SerializedObject(_config);
        serialized.FindProperty("_maxDistance").floatValue = -1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(_config.TryValidate(out _), Is.False);
        Assert.That(_config.TryResolveProfile(20, 0.02f, out _, out _), Is.False);
    }
}
#endif
