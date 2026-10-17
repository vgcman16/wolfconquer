using System.Security.Cryptography;

namespace WolfConquer.Core.Data;

public static class BackupSeedFileParser
{
    public static BackupSeedFile ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing backup seed file: {path}", path);
        }

        var bytes = File.ReadAllBytes(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new BackupSeedFile(
            Path.GetFileName(path),
            bytes.LongLength,
            sha256,
            Convert.ToBase64String(bytes));
    }
}
