using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Test-only files; no server configuration file is read or overwritten.
internal sealed class LegacyNativeDbReadOwnedFiles : IDisposable
{
    private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
    private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);
    internal string Root { get; }

    internal LegacyNativeDbReadOwnedFiles()
    {
        var parent = ArtifactsRoot();
        Root = Path.Combine(parent, "goal541-db-read-owned-" + Guid.NewGuid().ToString("N"));
        if (File.Exists(Root) || Directory.Exists(Root)) throw new IOException("Owned root already exists.");
        Directory.CreateDirectory(Root);
        OrdinaryChain(Root);
    }

    internal string Missing(string name = "rows.txt")
    {
        if (name is not ("rows.txt" or "other.txt" or "prefixrows.txt"))
            throw new IOException("Unregistered test leaf.");
        var path = Path.GetFullPath(Path.Combine(Root, name));
        if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), Root))
            throw new IOException("Outside owned root.");
        OrdinaryChain(path);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Expected missing leaf.");
        expected.Add(path, null);
        return path;
    }

    internal string Seed(string text, string name = "rows.txt")
    {
        var path = Missing(name);
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        expected[path] = bytes;
        return path;
    }

    internal void SetLocation(string location) => LegacyNativeServerConfig.DbLocation = location;

    internal static string ArtifactsRoot()
    {
        DirectoryInfo? workspace = new(AppContext.BaseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
            workspace = workspace.Parent;
        if (workspace is null) throw new InvalidOperationException("A real workspace is required.");
        var parent = Path.GetFullPath(Path.Combine(workspace.FullName, ".artifacts"));
        OrdinaryChain(parent);
        return parent;
    }

    internal static void OrdinaryChain(string path)
    {
        string? cursor = Path.GetFullPath(path);
        while (cursor is not null)
        {
            try
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse path rejected.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            cursor = Path.GetDirectoryName(cursor);
        }
    }

    public void Dispose()
    {
        LegacyNativeServerConfig.DbLocation = oldLocation;
        OrdinaryChain(Root);
        foreach (var path in Directory.GetFileSystemEntries(Root))
            if (!expected.ContainsKey(path) || Directory.Exists(path))
                throw new IOException("Unexpected owned entry preserved.");
        // Validate every leaf before deleting any file.
        foreach (var pair in expected)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(pair.Key), Root))
                throw new IOException("Owned leaf escaped.");
            OrdinaryChain(pair.Key);
            if (pair.Value is null)
            {
                if (File.Exists(pair.Key) || Directory.Exists(pair.Key))
                    throw new IOException("Missing control changed; preserving.");
            }
            else if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(File.ReadAllBytes(pair.Key)), SHA256.HashData(pair.Value)))
                throw new IOException("Changed test seed preserved.");
        }
        foreach (var pair in expected)
            if (pair.Value is not null) File.Delete(pair.Key);
        Directory.Delete(Root, recursive: false);
    }
}
