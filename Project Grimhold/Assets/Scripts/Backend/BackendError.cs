using System;

namespace Grimhold.Backend
{
    [Serializable]
    public struct BackendError
    {
        public string error;
        public string message;
    }
}
