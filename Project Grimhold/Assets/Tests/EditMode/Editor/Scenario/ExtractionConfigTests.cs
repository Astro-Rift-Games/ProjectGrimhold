using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public class ExtractionConfigTests
    {
        private ExtractionConfig _config;

        private static readonly FieldInfo CountdownDurationField =
            typeof(ExtractionConfig).GetField("_countdownDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CancelWhenLeavingAreaField =
            typeof(ExtractionConfig).GetField("_cancelWhenLeavingArea", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo BoundaryToleranceField =
            typeof(ExtractionConfig).GetField("_boundaryTolerance", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo RequireAliveToStartField =
            typeof(ExtractionConfig).GetField("_requireAliveToStart", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CancelWhenNotAliveField =
            typeof(ExtractionConfig).GetField("_cancelWhenNotAlive", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ExtractionConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void TryValidate_DefaultValues_Succeeds()
        {
            bool isValid = _config.TryValidate(out string error);
            Assert.That(isValid, Is.True, $"Default ExtractionConfig should be valid but failed with error: {error}");
            Assert.That(error, Is.Null);
        }

        [Test]
        public void Properties_HaveNoPublicSetters()
        {
            PropertyInfo[] properties = typeof(ExtractionConfig).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.That(properties, Is.Not.Empty, "ExtractionConfig must expose public properties.");

            foreach (PropertyInfo prop in properties)
            {
                MethodInfo setMethod = prop.GetSetMethod(nonPublic: false);
                Assert.That(setMethod, Is.Null, $"Property {prop.Name} should not have a public setter.");
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(-0.001f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryValidate_InvalidCountdownDuration_Fails(float invalidDuration)
        {
            Assert.That(CountdownDurationField, Is.Not.Null);
            CountdownDurationField.SetValue(_config, invalidDuration);

            bool isValid = _config.TryValidate(out string error);
            Assert.That(isValid, Is.False, $"TryValidate should fail for invalid CountdownDurationSeconds ({invalidDuration}).");
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(error, Does.Contain(nameof(ExtractionConfig.CountdownDurationSeconds)));
        }

        [TestCase(-0.1f)]
        [TestCase(-100f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void TryValidate_InvalidBoundaryTolerance_Fails(float invalidTolerance)
        {
            Assert.That(BoundaryToleranceField, Is.Not.Null);
            BoundaryToleranceField.SetValue(_config, invalidTolerance);

            bool isValid = _config.TryValidate(out string error);
            Assert.That(isValid, Is.False, $"TryValidate should fail for invalid BoundaryTolerance ({invalidTolerance}).");
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(error, Does.Contain(nameof(ExtractionConfig.BoundaryTolerance)));
        }

        [Test]
        public void TryValidate_ValidBoundaryToleranceZero_Succeeds()
        {
            Assert.That(BoundaryToleranceField, Is.Not.Null);
            BoundaryToleranceField.SetValue(_config, 0f);

            bool isValid = _config.TryValidate(out string error);
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(_config.BoundaryTolerance, Is.EqualTo(0f));
        }

        [TestCase(true, true, true)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(false, false, true)]
        public void BooleanPolicies_PreserveConfiguredValues(bool cancelLeaving, bool requireAlive, bool cancelNotAlive)
        {
            Assert.That(CancelWhenLeavingAreaField, Is.Not.Null);
            Assert.That(RequireAliveToStartField, Is.Not.Null);
            Assert.That(CancelWhenNotAliveField, Is.Not.Null);

            CancelWhenLeavingAreaField.SetValue(_config, cancelLeaving);
            RequireAliveToStartField.SetValue(_config, requireAlive);
            CancelWhenNotAliveField.SetValue(_config, cancelNotAlive);

            Assert.That(_config.CancelWhenLeavingArea, Is.EqualTo(cancelLeaving));
            Assert.That(_config.RequireAliveToStart, Is.EqualTo(requireAlive));
            Assert.That(_config.CancelWhenNotAlive, Is.EqualTo(cancelNotAlive));
        }

        [Test]
        public void ConfiguredAsset_ExistsAndIsValid()
        {
            string[] guids = AssetDatabase.FindAssets("t:ExtractionConfig");
            Assert.That(guids, Is.Not.Empty, "ExtractionConfig asset was not found in project assets.");

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            ExtractionConfig loadedAsset = AssetDatabase.LoadAssetAtPath<ExtractionConfig>(path);

            Assert.That(loadedAsset, Is.Not.Null, $"Failed to load ExtractionConfig asset at path: {path}");
            bool isValid = loadedAsset.TryValidate(out string error);
            Assert.That(isValid, Is.True, $"Loaded asset at {path} failed validation with error: {error}");
            Assert.That(error, Is.Null);
        }
    }
}
