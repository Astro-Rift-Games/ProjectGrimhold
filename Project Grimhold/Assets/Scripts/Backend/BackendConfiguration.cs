using UnityEngine;

namespace Grimhold.Backend
{
    [CreateAssetMenu(fileName = "BackendConfiguration", menuName = "Grimhold/Backend Configuration")]
    public class BackendConfiguration : ScriptableObject
    {
        [Tooltip("Base URL of the backend (e.g. http://localhost:3000)")]
        public string BaseUrl = "http://localhost:3000";

        [Tooltip("Timeout in seconds for HTTP requests")]
        public int TimeoutSeconds = 5;
    }
}
