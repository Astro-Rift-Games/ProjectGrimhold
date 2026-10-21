using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

/// <summary>
/// Pins how the request controllers read <see cref="RpcInvokeInfo"/> against the installed Fusion 2.1.1 runtime.
/// In this build <c>RpcBuilder.CheckSend</c> returns <see cref="RpcSendMessageResult.Sent"/> (0) for every
/// message it queues and the weaver only writes and sends the payload when <c>SendMessageResult</c> is zero,
/// so <c>MaskSent</c> (0x100) is never set on a real send. Do not replace the equality with a MaskSent check.
/// </summary>
public sealed class RpcAcceptanceContractTests
{
    private static readonly Type[] Controllers =
    {
        typeof(PlayerLootTransferNetworkController),
        typeof(PlayerLootDropNetworkController),
        typeof(PlayerWeaponEquipmentNetworkController),
        typeof(PlayerConsumableNetworkController)
    };

    // Every non-queued result RpcBuilder.CheckSend can return in the installed runtime.
    private static readonly RpcSendMessageResult[] RejectedSendResults =
    {
        RpcSendMessageResult.NotInvokableDuringResim,
        RpcSendMessageResult.PayloadSizeExceeded,
        RpcSendMessageResult.InsufficientSourceAuthority,
        RpcSendMessageResult.TargetPlayerIsLocalPlayer,
        RpcSendMessageResult.NoActiveConnections,
        RpcSendMessageResult.NoConnectionsWithAuthorityOrInterest,
        RpcSendMessageResult.TargetPlayerNotAvailable,
        RpcSendMessageResult.TargetObjectNotInPlayerInterest,
        RpcSendMessageResult.InsufficientTargetAuthority
    };

    [Test]
    public void InstalledFusion_QueuedSendIsZeroAndNeverCarriesTheSentMask()
    {
        Assert.That((int)RpcSendMessageResult.Sent, Is.EqualTo(0));
        Assert.That((int)RpcSendMessageResult.MaskSent, Is.EqualTo(0x100));
        Assert.That(((int)RpcSendMessageResult.Sent & (int)RpcSendMessageResult.MaskSent), Is.EqualTo(0));

        foreach (RpcSendMessageResult rejected in RejectedSendResults)
        {
            Assert.That((int)rejected, Is.Not.EqualTo(0), rejected.ToString());
        }
    }

    [Test]
    public void RemoteSend_IsAcceptedWithoutStateAuthority([ValueSource(nameof(Controllers))] Type controller)
    {
        var sent = new RpcInvokeInfo
        {
            LocalInvokeResult = RpcLocalInvokeResult.InsufficientTargetAuthority,
            SendMessageResult = RpcSendMessageResult.Sent
        };

        Assert.That(WasAccepted(controller, sent, false), Is.True);
    }

    [Test]
    public void LocalInvocation_IsAcceptedOnlyWithStateAuthority([ValueSource(nameof(Controllers))] Type controller)
    {
        var localOnly = new RpcInvokeInfo
        {
            LocalInvokeResult = RpcLocalInvokeResult.Invoked,
            SendMessageResult = RpcSendMessageResult.NoActiveConnections
        };

        Assert.That(WasAccepted(controller, localOnly, true), Is.True);
        Assert.That(WasAccepted(controller, localOnly, false), Is.False);
    }

    [Test]
    public void RejectedSend_IsNotAccepted([ValueSource(nameof(Controllers))] Type controller)
    {
        foreach (RpcSendMessageResult rejected in RejectedSendResults)
        {
            var notInvoked = new RpcInvokeInfo
            {
                LocalInvokeResult = RpcLocalInvokeResult.InsufficientSourceAuthority,
                SendMessageResult = rejected
            };

            Assert.That(WasAccepted(controller, notInvoked, false), Is.False, rejected.ToString());
            Assert.That(WasAccepted(controller, notInvoked, true), Is.False, rejected.ToString());
        }
    }

    private static bool WasAccepted(Type controller, in RpcInvokeInfo invokeInfo, bool hasStateAuthority)
    {
        MethodInfo method = controller.GetMethod("WasAccepted", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, controller.Name);
        return (bool)method.Invoke(null, new object[] { invokeInfo, hasStateAuthority });
    }
}
