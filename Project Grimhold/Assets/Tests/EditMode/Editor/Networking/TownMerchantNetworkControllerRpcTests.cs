using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

public class TownMerchantNetworkControllerRpcTests
{
    [TestCase("Rpc_RequestTrade")]
    [TestCase("Rpc_ReportTradeOutcome")]
    public void Requests_AreDeliveredOnlyToStateAuthority(string rpcName)
    {
        RpcAttribute rpc = GetRpc(rpcName, out _);

        Assert.That(rpc.Targets, Is.EqualTo(RpcTargets.StateAuthority));
    }

    [Test]
    public void TradeResponse_IsSentOnlyByStateAuthorityToTheRequester()
    {
        RpcAttribute rpc = GetRpc("Rpc_TradeResponse", out MethodInfo method);
        ParameterInfo target = method.GetParameters().First();

        Assert.That(rpc.Sources, Is.EqualTo(RpcSources.StateAuthority));
        Assert.That(target.ParameterType, Is.EqualTo(typeof(PlayerRef)));
        Assert.That(target.GetCustomAttribute<RpcTargetAttribute>(), Is.Not.Null);
    }

    [Test]
    public void TradeRequest_CarriesNoClientPrices()
    {
        GetRpc("Rpc_RequestTrade", out MethodInfo method);

        Assert.That(method.GetParameters().Select(p => p.ParameterType),
            Has.No.Member(typeof(MerchantPricedTradeLineMessage[])));
        Assert.That(typeof(MerchantTradeRequestLineMessage).GetFields().Select(f => f.Name),
            Is.EquivalentTo(new[] { "LootIndex", "Amount" }));
    }

    [TestCase("Rpc_RequestPurchase")]
    [TestCase("Rpc_RequestSale")]
    [TestCase("Rpc_PurchaseResponse")]
    [TestCase("Rpc_SaleResponse")]
    [TestCase("Rpc_ReportOperationOutcome")]
    public void SingleItemProtocol_IsGone(string rpcName)
    {
        Assert.That(typeof(TownMerchantNetworkController).GetMethod(rpcName, BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
    }

    [Test]
    public void Controller_KeepsNoPerPlayerPurchaseTracking()
    {
        FieldInfo field = typeof(TownMerchantNetworkController)
            .GetField("_mySessionPurchases", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Null);
    }

    private static RpcAttribute GetRpc(string name, out MethodInfo method)
    {
        method = typeof(TownMerchantNetworkController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);

        RpcAttribute rpc = method.GetCustomAttribute<RpcAttribute>();
        Assert.That(rpc, Is.Not.Null, name);
        return rpc;
    }
}
