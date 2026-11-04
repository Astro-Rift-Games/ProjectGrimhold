using UnityEngine.Networking;

namespace Grimhold.Backend
{
    /// <summary>Applied on the shared send path of every Unity backend client, including host calls.</summary>
    public static class InventoryProtocol
    {
        public const string Header = "X-Grimhold-Inventory-Protocol";
        public const string Version = "2";

        public static UnityWebRequest Create(string url, string method)
        {
            var request = new UnityWebRequest(url, method);
            Apply(request);
            return request;
        }

        public static UnityWebRequest Get(string url)
        {
            var request = UnityWebRequest.Get(url);
            Apply(request);
            return request;
        }

        public static UnityWebRequest Delete(string url)
        {
            var request = UnityWebRequest.Delete(url);
            Apply(request);
            return request;
        }

        public static void Apply(UnityWebRequest request) => request.SetRequestHeader(Header, Version);
    }
}
