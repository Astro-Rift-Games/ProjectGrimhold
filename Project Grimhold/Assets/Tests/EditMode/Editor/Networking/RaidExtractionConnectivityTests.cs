#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Networking
{
    [TestFixture]
    public sealed class RaidExtractionConnectivityTests
    {
        private const string ExtractionControllerPath =
            "Assets/Scripts/Player/Extraction/PlayerExtractionController.cs";
        private const string SpawnManagerPath =
            "Assets/Scripts/Networking/NetworkSpawnManager.cs";

        [Test]
        public void CancellationReason_ContainsParticipantDisconnected()
        {
            Type controllerType = typeof(PlayerExtractionController);
            Type cancellationReasonType = controllerType.GetNestedType("CancellationReason", BindingFlags.NonPublic);
            Assert.That(cancellationReasonType, Is.Not.Null, "CancellationReason enum must be defined inside PlayerExtractionController.");

            bool hasDisconnectedReason = Enum.IsDefined(cancellationReasonType, "ParticipantDisconnected");
            Assert.That(hasDisconnectedReason, Is.True, "CancellationReason must define ParticipantDisconnected.");
        }

        [Test]
        public void TryBeginExtraction_ChecksParticipantConnectivity()
        {
            string source = File.ReadAllText(ExtractionControllerPath);
            int beginIndex = source.IndexOf("public bool TryBeginExtraction", StringComparison.Ordinal);
            Assert.That(beginIndex, Is.GreaterThanOrEqualTo(0));

            int nextMethodIndex = source.IndexOf("public void NotifyExtractionZoneExit", beginIndex, StringComparison.Ordinal);
            Assert.That(nextMethodIndex, Is.GreaterThan(beginIndex));

            string methodBody = source.Substring(beginIndex, nextMethodIndex - beginIndex);
            Assert.That(methodBody, Does.Contain("IsParticipantConnected()"));
        }

        [Test]
        public void EvaluateContinuation_CancelsOnParticipantDisconnected()
        {
            string source = File.ReadAllText(ExtractionControllerPath);
            int evalIndex = source.IndexOf("private bool EvaluateContinuation", StringComparison.Ordinal);
            Assert.That(evalIndex, Is.GreaterThanOrEqualTo(0));

            int nextMethodIndex = source.IndexOf("private bool CanUseSanctuaryZone", evalIndex, StringComparison.Ordinal);
            Assert.That(nextMethodIndex, Is.GreaterThan(evalIndex));

            string methodBody = source.Substring(evalIndex, nextMethodIndex - evalIndex);
            Assert.That(methodBody, Does.Contain("IsParticipantConnected()"));
            Assert.That(methodBody, Does.Contain("CancellationReason.ParticipantDisconnected"));
        }

        [Test]
        public void FallbackWithoutDecider_RemovesParticipantFromDisconnectedRegistryAndUpdatesGate()
        {
            string source = File.ReadAllText(SpawnManagerPath);
            int fallbackIndex = source.IndexOf("private void ReevaluateRetainedParticipantsFallback", StringComparison.Ordinal);
            Assert.That(fallbackIndex, Is.GreaterThanOrEqualTo(0));

            int nextMethodIndex = source.IndexOf("private static bool IsAvatarDowned", fallbackIndex, StringComparison.Ordinal);
            Assert.That(nextMethodIndex, Is.GreaterThan(fallbackIndex));

            string methodBody = source.Substring(fallbackIndex, nextMethodIndex - fallbackIndex);
            Assert.That(methodBody, Does.Contain("_disconnectedRegistry.Remove"));
            Assert.That(methodBody, Does.Contain("UpdateMatchSessionGate"));
        }
    }
}
#endif
