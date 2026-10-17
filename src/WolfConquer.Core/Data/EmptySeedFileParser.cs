using System.Security.Cryptography;

namespace WolfConquer.Core.Data;

public static class EmptySeedFileParser
{
    public static EmptySeedFile ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing empty seed file: {path}", path);
        }

        var bytes = File.ReadAllBytes(path);
        var sourceFile = Path.GetFileName(path);

        if (bytes.Length != 0)
        {
            throw new InvalidDataException($"{sourceFile} expected to be empty but contained {bytes.LongLength} bytes.");
        }

        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return new EmptySeedFile(
            sourceFile,
            bytes.LongLength,
            sha256,
            Convert.ToBase64String(bytes));
    }
}
