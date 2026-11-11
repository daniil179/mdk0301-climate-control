using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace Climate.Operations;

public static class BackupService
{
    public static string Create(string sourcePath, string destinationPath)
    {
        if (Path.GetFullPath(sourcePath) == Path.GetFullPath(destinationPath)) throw new ArgumentException("Backup must be separate.");
        if (File.Exists(destinationPath)) throw new IOException("Backup destination already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        // Release native handles before hashing/copying the backup on Windows.
        using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = destinationPath, Pooling = false }.ToString()))
        {
            source.Open(); destination.Open(); source.BackupDatabase(destination);
            using var check = destination.CreateCommand();check.CommandText = "PRAGMA integrity_check";
            if ((string?)check.ExecuteScalar() != "ok") throw new InvalidDataException("Backup integrity check failed.");
        }
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destinationPath))).ToLowerInvariant();
        File.WriteAllText(destinationPath + ".sha256", hash); return hash;
    }
    public static void Restore(string backup, string target, string expectedHash)
    {
        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(backup))).ToLowerInvariant();
        if (actual != expectedHash) throw new InvalidDataException("Backup hash mismatch.");
        if (File.Exists(target)) throw new IOException("Restore target already exists.");
        File.Copy(backup, target);
    }
}
