using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBinaryFileTests
{
    private static readonly FieldInfo StreamField = typeof(LegacyNativeBinaryFile)
        .GetField("pFile", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static FileStream Stream(LegacyNativeBinaryFile file) =>
        Assert.IsType<FileStream>(StreamField.GetValue(file));

    private static void Exclusive(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.CanRead);
    }

    private static void StillLocked(string path) => Assert.Throws<IOException>(() => Exclusive(path));

    private static unsafe bool ReadBytes(LegacyNativeBinaryFile file, int amount)
    {
        Assert.True(amount <= 16, "Test buffer bounds must remain safe.");
        byte* buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xA5);
        return file.Read(buffer, amount);
    }

    private static unsafe bool WriteBytes(LegacyNativeBinaryFile file, int amount)
    {
        Assert.True(amount <= 16, "Test buffer bounds must remain safe.");
        byte* buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0x5A);
        return file.Write(buffer, amount);
    }

    private static unsafe bool SizedRead(LegacyNativeBinaryFile file, int count, int size)
    {
        Assert.True(unchecked(count * size) <= 16, "Only bounded or rejected amounts are tested.");
        byte* buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0xA5);
        return file.Read(buffer, count, size);
    }

    private static unsafe bool SizedWrite(LegacyNativeBinaryFile file, int count, int size)
    {
        Assert.True(unchecked(count * size) <= 16, "Only bounded or rejected amounts are tested.");
        byte* buffer = stackalloc byte[16];
        new Span<byte>(buffer, 16).Fill(0x5A);
        return file.Write(buffer, count, size);
    }

    private sealed class OwnedFiles : IDisposable
    {
        private readonly string parent = LegacyNativeDbReadOwnedFiles.ArtifactsRoot();
        private readonly string root;
        private readonly List<LegacyNativeBinaryFile> owners = new();
        private readonly HashSet<FileStream> streams = new();
        private readonly HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);

        internal OwnedFiles()
        {
            Assert.True(OperatingSystem.IsWindows(), "Genuine BinaryFile I/O requires Windows Kernel32.");
            root = Path.Combine(parent, "native-binary-file-" + Guid.NewGuid().ToString("N"));
            Guard();
            Assert.False(Directory.Exists(root));
            Assert.False(File.Exists(root));
            Directory.CreateDirectory(root);
        }

        internal string PathFor(string name)
        {
            Assert.Contains(name, new[] { "first.bin", "second.bin", "missing.bin" });
            var path = Path.Combine(root, name);
            Assert.Equal(root, Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
            paths.Add(path);
            return path;
        }

        internal string Seed(byte[] bytes, string name = "first.bin")
        {
            var path = PathFor(name);
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(bytes);
            file.Flush(true);
            return path;
        }

        internal LegacyNativeBinaryFile Track(LegacyNativeBinaryFile file)
        {
            owners.Add(file);
            Capture(file);
            return file;
        }

        internal void Capture(LegacyNativeBinaryFile file)
        {
            if (StreamField.GetValue(file) is FileStream stream) streams.Add(stream);
        }

        internal LegacyNativeBinaryFile Open(string path, FileMode mode = FileMode.Open)
        {
            Assert.Contains(path, paths);
            var file = Track(new LegacyNativeBinaryFile());
            Assert.True(file.Open(path, mode));
            Capture(file);
            return file;
        }

        public void Dispose()
        {
            // Retain every actual FileStream, including one abandoned by native Open.
            // This test-only cleanup does not change production lifetime semantics.
            foreach (var owner in owners)
            {
                Capture(owner);
                if (owner.Success) owner.Close();
            }
            foreach (var stream in streams) stream.Dispose();
            Guard();
            var entries = Directory.GetFileSystemEntries(root);
            foreach (var entry in entries)
            {
                Assert.Contains(entry, paths);
                Assert.False(Directory.Exists(entry));
                LegacyNativeDbReadOwnedFiles.OrdinaryChain(entry);
            }
            foreach (var entry in entries) File.Delete(entry);
            Directory.Delete(root, recursive: false);
        }

        private void Guard()
        {
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-binary-file-[0-9a-f]{32}$", Path.GetFileName(root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(root);
        }
    }
}
