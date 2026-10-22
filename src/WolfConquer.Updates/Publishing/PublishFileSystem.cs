using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

internal static class PublishFileSystem
{
    internal static string Root(string path, bool mustExist)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new InvalidDataException("Publication requires explicit local absolute roots.");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (string.Equals(full, Path.GetPathRoot(full), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A volume root is not a publication directory.");
        Check(full);
        if (File.Exists(full)) throw new IOException("Publication root names a file.");
        if (mustExist && !Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        return full;
    }

    internal static bool Contains(string parent, string child) =>
        string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) ||
        child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    internal static string Contained(string root, string relative)
    {
        UpdatePathPolicy.ValidateRelativePath(relative);
        string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!Contains(root, full)) throw new InvalidDataException("Publication path escaped its root.");
        Check(full);
        return full;
    }

    internal static void Check(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reparse points are not permitted in publication paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    internal static void EnsureDirectory(string path)
    {
        Check(path); Directory.CreateDirectory(path); Check(path);
    }

    internal static FileStream Read(string path)
    {
        Check(path);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.SequentialScan);
    }

    internal static FileStream New(string path)
    {
        Check(path);
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920,
            FileOptions.WriteThrough);
    }

    internal static void VerifyBlob(string path, long length, string hash)
    {
        using var stream = Read(path);
        if (stream.Length != length || !Convert.ToHexString(SHA256.HashData(stream)).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("An existing publication blob has unexpected bytes.");
        if (stream.Length != length || stream.Position != length)
            throw new InvalidDataException("Publication blob changed while it was verified.");
        Check(path);
    }

    internal static void DeleteOwnedTemporary(string path)
    {
        Check(path);
        if (File.Exists(path)) File.Delete(path);
    }
}
