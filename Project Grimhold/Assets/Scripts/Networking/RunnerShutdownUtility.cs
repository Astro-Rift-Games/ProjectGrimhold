using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public static class RunnerShutdownUtility
{
    /// <summary>
    /// Shuts down and destroys the captured runner composition. The captured identity prevents
    /// a late completion from affecting a replacement runner created by the same launcher.
    /// </summary>
    public static async Task<bool> ShutdownAndDestroyAsync(
        NetworkRunner runner,
        GameObject runnerObject)
    {
        bool succeeded = true;
        try
        {
            if (runner != null && runner.IsRunning)
            {
                await runner.Shutdown(destroyGameObject: false);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            succeeded = false;
        }

        if (runnerObject != null)
        {
            Object.Destroy(runnerObject);

            // Destruction is deferred until the player loop advances. Do not allow the
            // coordinator to create a replacement runner while the old object is alive.
            while (runnerObject != null)
            {
                await Task.Yield();
            }
        }

        return succeeded;
    }
}
