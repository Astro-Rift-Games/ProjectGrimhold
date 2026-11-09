using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Automatically ensures Git pre-push quality hooks are configured whenever Unity Editor opens,
/// without requiring manual developer intervention.
/// </summary>
[InitializeOnLoad]
public static class GitHooksSetup
{
    private const string SessionKey = "ProjectGrimhold_GitHooksConfigured";

    static GitHooksSetup()
    {
        EditorApplication.delayCall += EnsureGitHooksConfigured;
    }

    private static void EnsureGitHooksConfigured()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        try
        {
            string projectDir = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectDir)) return;

            string repoRoot = Directory.GetParent(projectDir)?.FullName;
            if (string.IsNullOrEmpty(repoRoot)) return;

            string hooksDir = Path.Combine(repoRoot, ".githooks");
            if (!Directory.Exists(hooksDir)) return;

            var checkInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "config core.hooksPath",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(checkInfo);
            if (process == null) return;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(3000);

            if (!string.Equals(output, ".githooks", StringComparison.OrdinalIgnoreCase))
            {
                var setInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "config core.hooksPath .githooks",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var setProcess = Process.Start(setInfo);
                setProcess?.WaitForExit(3000);
                UnityEngine.Debug.Log("[GitHooksSetup] Automatically configured Git pre-push quality hooks.");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[GitHooksSetup] Could not verify git hooks: {ex.Message}");
        }
    }
}
