using System;
using System.IO;

/// <summary>
/// Atomic local-file implementation for the profile repository.
/// </summary>
public sealed class LocalProfileFileStore : ILocalProfileFileStore
{
    public bool Exists(string path) => File.Exists(path);

    public bool TryRead(string path, out string contents, out string error)
    {
        try
        {
            contents = File.ReadAllText(path);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            contents = null;
            error = exception.Message;
            return false;
        }
    }

    public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
    {
        try
        {
            string directory = Path.GetDirectoryName(mainPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, contents);
            if (File.Exists(mainPath))
            {
                File.Replace(temporaryPath, mainPath, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, mainPath);
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
    {
        try
        {
            string temporaryPath = mainPath + ".recovery.tmp";
            File.Copy(backupPath, temporaryPath, true);
            File.Replace(temporaryPath, mainPath, null, true);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
