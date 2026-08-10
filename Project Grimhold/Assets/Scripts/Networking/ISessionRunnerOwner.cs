using System;
using System.Threading.Tasks;
using Fusion;

public interface ISessionRunnerOwner
{
    NetworkRunner Runner { get; }
    event Action<NetworkRunner, ShutdownReason> RunnerShutdownObserved;
    Task<bool> ShutdownAndDestroyRunnerAsync();
}
