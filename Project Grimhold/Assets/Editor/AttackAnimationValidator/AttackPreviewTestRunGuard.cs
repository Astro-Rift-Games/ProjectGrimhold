#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

/// <summary>Observe all Test Runner instances, including windows opened partway through a run.
/// Registered once per editor domain; no test execution or test result mutation.</summary>
[InitializeOnLoad]
internal static class AttackPreviewTestRunGuard
{
    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { IsRunning = true; Changed?.Invoke(); }
        public void RunFinished(ITestResultAdaptor result) { IsRunning = false; Changed?.Invoke(); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
    private static readonly Callbacks Listener = new Callbacks();
    public static bool IsRunning { get; private set; }
    public static event Action Changed;
    static AttackPreviewTestRunGuard()
    {
        TestRunnerApi.RegisterTestCallback(Listener, int.MaxValue);
        AssemblyReloadEvents.beforeAssemblyReload += Unregister;
    }
    private static void Unregister() => TestRunnerApi.UnregisterTestCallback(Listener);
}
#endif
