/// <summary>
/// Filesystem boundary used by the local profile repository.
/// </summary>
public interface ILocalProfileFileStore
{
    bool Exists(string path);
    bool TryRead(string path, out string contents, out string error);
    bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error);
    bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error);
}
