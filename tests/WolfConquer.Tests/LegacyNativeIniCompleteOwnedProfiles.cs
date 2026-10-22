using System.Reflection;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Only test-owned absolute profiles. Never uses a user/server INI or recursive deletion.
internal sealed class LegacyNativeIniCompleteOwnedProfiles : IDisposable
{
    private readonly string root;
    private readonly Dictionary<string, byte[]?> expected = new(StringComparer.OrdinalIgnoreCase);

    public LegacyNativeIniCompleteOwnedProfiles()
    {
        DirectoryInfo? workspace = new DirectoryInfo(AppContext.BaseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
            workspace = workspace.Parent;
        if (workspace is null) throw new InvalidOperationException("Owned workspace not found.");
        var artifacts = Path.Combine(workspace.FullName, ".artifacts");
        CheckChain(artifacts, true);
        Directory.CreateDirectory(artifacts);
        root = Path.Combine(artifacts, "goal539-ini-test-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Owned leaf already exists.");
        Directory.CreateDirectory(root);
        CheckChain(root, false);
    }

    public string MissingPath()
    {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".ini");
        expected.Add(path, null);
        Guard(path, true);
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("Expected absence.");
        return path;
    }

    public string Seed(string text, bool unicode = false)
    {
        var path = MissingPath();
        var encoding = unicode ? Encoding.Unicode : Encoding.ASCII;
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        expected[path] = bytes;
        Assert.Equal(bytes, File.ReadAllBytes(path));
        return path;
    }

    public LegacyNativeItemCounterShellIni Adapter(string path)
    {
        Guard(path, expected[path] is null);
        return new LegacyNativeItemCounterShellIni(path, overridePath: true);
    }

    // Call only AFTER assertions establish an intended native write's effects.
    // A failed assertion before this call preserves changed files instead of silently accepting cleanup.
    public void CaptureExpectedMutation(string path)
    {
        Guard(path, false);
        expected[path] = File.ReadAllBytes(path);
    }

    public byte[] Bytes(string path)
    {
        Guard(path, false);
        return File.ReadAllBytes(path);
    }

    private void Guard(string path, bool allowMissingLeaf)
    {
        if (!expected.ContainsKey(path) || !StringComparer.OrdinalIgnoreCase.Equals(
            Path.GetDirectoryName(Path.GetFullPath(path)), root))
            throw new IOException("Not a registered owned leaf.");
        CheckChain(root, false);
        CheckChain(path, allowMissingLeaf);
    }

    private static void CheckChain(string path, bool allowMissingLeaf)
    {
        var first = true;
        for (string? cursor = Path.GetFullPath(path); cursor is not null; cursor = Path.GetDirectoryName(cursor))
        {
            try
            {
                if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse point rejected: " + cursor);
            }
            catch (FileNotFoundException) when (first && allowMissingLeaf) { }
            catch (DirectoryNotFoundException) when (first && allowMissingLeaf) { }
            first = false;
        }
    }

    public void Dispose()
    {
        CheckChain(root, false);
        foreach (var actual in Directory.GetFileSystemEntries(root))
            if (!expected.ContainsKey(actual)) throw new IOException("Unregistered owned-directory entry.");
        // Validate ALL leaves before deleting ANY leaf.
        foreach (var pair in expected)
        {
            Guard(pair.Key, pair.Value is null);
            if (pair.Value is null)
            {
                if (File.Exists(pair.Key) || Directory.Exists(pair.Key))
                    throw new IOException("A registered absent leaf changed; preserving evidence.");
            }
            else if (!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)))
                throw new IOException("Owned bytes changed unexpectedly; preserving evidence.");
        }
        foreach (var pair in expected)
            if (pair.Value is not null) File.Delete(pair.Key);
        Directory.Delete(root, recursive: false);
    }
}

internal sealed class LegacyNativeIniCompleteParserLease : IDisposable
{
    private readonly Dictionary<FieldInfo, object?> original = typeof(LegacyNativeItemCounterShellIni)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Func<,>))
        .ToDictionary(f => f, f => f.GetValue(null));

    public void Set(string name, object? value) =>
        typeof(LegacyNativeItemCounterShellIni).GetField(name)!.SetValue(null, value);

    public void Dispose()
    {
        foreach (var pair in original) pair.Key.SetValue(null, pair.Value);
    }
}
