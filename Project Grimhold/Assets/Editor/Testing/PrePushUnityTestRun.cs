using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Runs the pre-push EditMode test subset inside the open Editor and reports the outcome to a file,
/// so the Git hook can poll it from a shell. Invoked with:
/// unity command script eval 'return PrePushUnityTestRun.Start();'
/// </summary>
public static class PrePushUnityTestRun
{
    public const string ResultPath = "Temp/PrePushUnityTests.json";

    [Serializable]
    private sealed class Failure
    {
        public string name;
        public string message;
    }

    [Serializable]
    private sealed class Report
    {
        public string status;
        public int total;
        public int passed;
        public int failed;
        public List<Failure> failures = new List<Failure>();
    }

    private static TestRunnerApi _api;
    private static Callbacks _callbacks;
    private static bool _running;

    public static string Start()
    {
        // A second push while a run is executing waits for it instead of queueing another run.
        if (_running) return "already-running";

        _running = true;
        try
        {
            Write(new Report { status = "running" });

            if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RetrieveTestList(TestMode.EditMode, root =>
            {
                try
                {
                    StartSelected(root);
                }
                catch (Exception exception)
                {
                    Fail(exception);
                }
            });
        }
        catch (Exception exception)
        {
            Fail(exception);
            return "failed";
        }

        return "started";
    }

    // Any failure must clear _running and report, or every later push would wait on a run that never ends.
    private static void Fail(Exception exception)
    {
        _running = false;
        Write(new Report { status = "error", failures = { new Failure { name = "runner", message = exception.Message } } });
    }

    private static void StartSelected(ITestAdaptor root)
    {
        var names = new List<string>();
        Collect(root, names);
        if (names.Count == 0)
        {
            _running = false;
            Write(new Report { status = "error", failures = { new Failure { name = "selection", message = "No EditMode tests selected." } } });
            return;
        }

        if (_callbacks != null) _api.UnregisterCallbacks(_callbacks);
        _callbacks = new Callbacks();
        _api.RegisterCallbacks(_callbacks);
        _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() }));
    }

    private static void Collect(ITestAdaptor test, List<string> names)
    {
        if (test == null || test.RunState == RunState.Explicit) return;
        if (!test.IsSuite && !test.HasChildren)
        {
            if (!PrePushTestSelection.IsExcluded(test.FullName)) names.Add(test.FullName);
            return;
        }

        foreach (ITestAdaptor child in test.Children) Collect(child, names);
    }

    private static void Summarize(ITestResultAdaptor result, Report report)
    {
        if (result.HasChildren)
        {
            foreach (ITestResultAdaptor child in result.Children) Summarize(child, report);
            return;
        }

        report.total++;
        if (result.TestStatus == TestStatus.Passed) report.passed++;
        else if (result.TestStatus == TestStatus.Failed)
        {
            report.failed++;
            report.failures.Add(new Failure { name = result.FullName, message = result.Message });
        }
    }

    private static void Write(Report report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        File.WriteAllText(ResultPath, JsonUtility.ToJson(report));
    }

    private sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                var report = new Report { status = "completed" };
                Summarize(result, report);
                Write(report);
                _running = false;
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }
    }
}
