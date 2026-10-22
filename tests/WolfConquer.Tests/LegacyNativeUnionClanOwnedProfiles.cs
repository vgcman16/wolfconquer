using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Only registered numeric INIs under this fresh GUID root. Real Win32 profile
// APIs are used by production and verification; cleanup is never recursive.
internal sealed class LegacyNativeUnionClanOwnedProfiles : IDisposable
{
    private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
    private readonly string root;
    private readonly List<string> directories = new();
    private readonly Dictionary<string, byte[]> expected = new(StringComparer.OrdinalIgnoreCase);
    internal string SecondDatabase => Path.Combine(root, "second");
    internal string MissingDatabase => Path.Combine(root, "never-created");

    internal LegacyNativeUnionClanOwnedProfiles()
    {
        root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), "native-union-clan-" + Guid.NewGuid().ToString("N"));
        if (File.Exists(root) || Directory.Exists(root)) throw new IOException("Owned root already exists.");
        CreateDirectory(root);
        CreateDirectory(Path.Combine(root, "Users"));
        LegacyNativeServerConfig.DbLocation = root;
    }

    internal string Seed(uint uid, string text, bool unicode = false, bool secondDatabase = false)
    {
        var database = secondDatabase ? SecondDatabase : root;
        if (secondDatabase && !directories.Contains(database))
        {
            CreateDirectory(database);
            CreateDirectory(Path.Combine(database, "Users"));
        }
        var path = Path.Combine(database, "Users", uid.ToString(CultureInfo.CurrentCulture) + ".ini");
        Validate(path);
        if (File.Exists(path) || Directory.Exists(path) || expected.ContainsKey(path)) throw new IOException("Owned leaf already exists.");
        var encoding = unicode ? Encoding.Unicode : Encoding.ASCII;
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        expected.Add(path, bytes);
        return path;
    }

    internal string Read(string path, string key, string section = "Character")
    {
        ValidateRegistered(path);
        return new LegacyNativeItemCounterShellIni(path, overridePath: true).ReadString(section, key, "__absent__");
    }

    internal void CaptureExpectedMutation(string path)
    {
        ValidateRegistered(path);
        expected[path] = File.ReadAllBytes(path);
    }

    private void CreateDirectory(string path)
    {
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        Directory.CreateDirectory(path);
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        directories.Add(path);
    }

    private void Validate(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !directories.Contains(Path.GetDirectoryName(full)!))
            throw new IOException("Not an owned leaf.");
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(full);
    }

    private void ValidateRegistered(string path)
    {
        Validate(path);
        if (!expected.ContainsKey(path)) throw new IOException("Unregistered owned leaf.");
    }

    public void Dispose()
    {
        LegacyNativeServerConfig.DbLocation = priorLocation;
        foreach (var directory in directories)
        {
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(directory);
            foreach (var entry in Directory.GetFileSystemEntries(directory))
                if (!directories.Contains(entry) && !expected.ContainsKey(entry))
                    throw new IOException("Unexpected owned entry preserved.");
        }
        foreach (var pair in expected)
        {
            ValidateRegistered(pair.Key);
            if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)))
                throw new IOException("Unexpected owned bytes preserved.");
        }
        foreach (var path in expected.Keys) File.Delete(path);
        for (var index = directories.Count - 1; index >= 0; index--)
            Directory.Delete(directories[index], recursive: false);
    }
}
