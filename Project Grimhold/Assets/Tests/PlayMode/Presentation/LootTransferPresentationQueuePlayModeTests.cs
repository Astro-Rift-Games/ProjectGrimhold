#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Presentation
{
    public sealed class LootTransferPresentationQueuePlayModeTests
    {
        private GameObject _holder;
        private PlayerLootTransferNetworkController _controller;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("LootTransferPresentationQueueTest");
            _controller = _holder.AddComponent<PlayerLootTransferNetworkController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        [UnityTest]
        public IEnumerator Notifications_AreDeferredAndConfirmationFollowsFinalization()
        {
            var notifications = new List<string>();
            _controller.RequestInFlightChanged += value => notifications.Add($"pending:{value}");
            _controller.TransferConfirmed += _ => notifications.Add("confirmation");

            Assert.That(_controller.DebugStageAcceptedRequestForPresentation(new EntityId(50), new EntityId(60), 2, out uint sequence), Is.True);
            Assert.That(_controller.HasRequestInFlight, Is.True);
            Assert.That(notifications, Is.Empty);

            _controller.Render();
            Assert.That(notifications, Is.EqualTo(new[] { "pending:True" }));
            notifications.Clear();

            var request = new LootTransferRequest(
                new EntityId(50),
                new EntityId(60),
                new LootId("coin"),
                4,
                10);
            LootTransferResult result = LootTransferResult.Succeeded(request);
            var confirmation = new LootTransferConfirmation(
                sequence,
                request.SourceId,
                request.DestinationId,
                2,
                request.SimulationTick,
                result,
                request.LootId);

            Assert.That(_controller.DebugStageRequestCompletionForPresentation(sequence, true, confirmation), Is.True);
            Assert.That(_controller.HasRequestInFlight, Is.False);
            Assert.That(notifications, Is.Empty);

            _controller.Render();
            Assert.That(notifications, Is.EqualTo(new[] { "pending:False", "confirmation" }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectionPublishesOnlyFinalizationAndResetDiscardsQueuedState()
        {
            var notifications = new List<string>();
            _controller.RequestInFlightChanged += value => notifications.Add($"pending:{value}");
            _controller.TransferConfirmed += _ => notifications.Add("confirmation");

            _controller.DebugStageAcceptedRequestForPresentation(new EntityId(50), new EntityId(60), 2, out uint sequence);
            _controller.Render();
            notifications.Clear();
            Assert.That(_controller.DebugStageRequestCompletionForPresentation(sequence, false, default), Is.True);
            _controller.Render();
            Assert.That(notifications, Is.EqualTo(new[] { "pending:False" }));

            notifications.Clear();
            _controller.DebugStageAcceptedRequestForPresentation(new EntityId(51), new EntityId(60), 3, out _);
            _controller.DebugResetLocalPresentationState();
            Assert.That(_controller.HasRequestInFlight, Is.False);
            _controller.Render();
            Assert.That(notifications, Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TransportRejection_ReleasesRequestBeforePublishingFeedback()
        {
            var notifications = new List<string>();
            _controller.RequestInFlightChanged += value => notifications.Add($"pending:{value}");
            _controller.TransportRejected += reason => notifications.Add($"rejection:{reason}");

            Assert.That(_controller.DebugStageAcceptedRequestForPresentation(new EntityId(50), new EntityId(60), 2, out uint sequence), Is.True);
            _controller.Render();
            notifications.Clear();

            Assert.That(
                _controller.DebugStageTransportRejectionForPresentation(
                    sequence,
                    LootTransferTransportRejectionReason.DependenciesUnavailable),
                Is.True);
            Assert.That(_controller.HasRequestInFlight, Is.False);
            Assert.That(notifications, Is.Empty);

            _controller.Render();
            Assert.That(
                notifications,
                Is.EqualTo(new[] { "pending:False", "rejection:DependenciesUnavailable" }));
            yield return null;
        }

        [Test]
        public void RpcAcceptance_InputOnlyPeerRequiresRemoteSend()
        {
            var localOnly = new RpcInvokeInfo
            {
                LocalInvokeResult = RpcLocalInvokeResult.Invoked
            };
            var remoteSent = new RpcInvokeInfo
            {
                SendMessageResult = RpcSendMessageResult.Sent
            };

            Assert.That(InvokeWasAccepted(localOnly, false), Is.False);
            Assert.That(InvokeWasAccepted(localOnly, true), Is.True);
            Assert.That(InvokeWasAccepted(remoteSent, false), Is.True);
        }

        [Test]
        public void FullStackRpc_IdentifiesLocalHostAsInputAuthorityPlayer()
        {
            MethodInfo method = typeof(PlayerLootTransferNetworkController).GetMethod(
                "RPC_RequestFullStack",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            RpcAttribute attribute = method.GetCustomAttribute<RpcAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.HostMode, Is.EqualTo(RpcHostMode.SourceIsHostPlayer));
        }

        private static bool InvokeWasAccepted(in RpcInvokeInfo invokeInfo, bool hasStateAuthority)
        {
            MethodInfo method = typeof(PlayerLootTransferNetworkController).GetMethod(
                "WasAccepted",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { invokeInfo, hasStateAuthority });
        }
    }
}
#endif
