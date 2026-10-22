using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientProficiencyPersistenceTests
{
    internal static byte[] Row(uint id, uint level = 0, byte previous = 0,
        uint experience = 0, uint needed = 0, byte padding = 0)
    {
        var bytes = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, id);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), level);
        bytes[8] = previous;
        bytes[9] = padding; bytes[10] = padding; bytes[11] = padding;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), needed);
        return bytes;
    }

    internal static byte[] Image(int count, params byte[][] rows) =>
        BitConverter.GetBytes(count).Concat(rows.SelectMany(row => row)).ToArray();

    internal static byte[] Bytes<T>(ref T row) where T : struct =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref row, 1)).ToArray();

    private static LegacyNativeGameClientRuntime Client(bool profs = true)
    {
        var client = new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false };
        client.Player.UID = 1293700;
        if (profs) client.MyProfs = new LegacyNativeProficiencyStorage(client);
        return client;
    }

    private static void AssertClosed(string path)
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    private static void AssertStillOpen(string path) => Assert.Throws<IOException>(() =>
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    private static byte[] ReadWhileOpen(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    }

    internal sealed class OwnedProfs : IDisposable
    {
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly string root;
        private readonly List<LegacyNativeItemRecordFile> files = new();
        internal string DirectoryPath { get; }
        internal string FilePath { get; }

        internal OwnedProfs(bool createDirectory = true)
        {
            Assert.True(OperatingSystem.IsWindows(), "Actual proficiency I/O requires Windows Kernel32.");
            root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), "native-client-prof-" + Guid.NewGuid().ToString("N"));
            DirectoryPath = Path.Combine(root, "PlayersProfs");
            FilePath = Path.Combine(DirectoryPath, "1293700.bin");
            Guard();
            Assert.False(Directory.Exists(root));
            Directory.CreateDirectory(root);
            if (createDirectory) Directory.CreateDirectory(DirectoryPath);
            LegacyNativeServerConfig.DbLocation = root;
        }

        internal void Seed(byte[] bytes)
        {
            Guard();
            using var stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        }

        internal LegacyNativeItemRecordFile Open(FileMode mode)
        {
            Guard();
            var file = new LegacyNativeItemRecordFile();
            files.Add(file);
            Assert.True(file.Open(FilePath, mode));
            return file;
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = oldLocation;
            // Retain each real owner, including faults which intentionally skip Close.
            // Test cleanup is explicit; no production finally or GC recovery is added.
            foreach (var file in files)
                if (file.Success) file.Close();
            Guard();
            if (Directory.Exists(DirectoryPath))
                Assert.All(Directory.GetFileSystemEntries(DirectoryPath), path => Assert.Equal(FilePath, path));
            Assert.All(Directory.GetFileSystemEntries(root), path => Assert.Equal(DirectoryPath, path));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: false);
            Directory.Delete(root, recursive: false);
        }

        private void Guard()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-client-prof-[0-9a-f]{32}$", Path.GetFileName(root));
            Assert.Equal(root, Path.GetDirectoryName(DirectoryPath));
            Assert.Equal(DirectoryPath, Path.GetDirectoryName(FilePath));
            Assert.Equal("1293700.bin", Path.GetFileName(FilePath));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(FilePath);
        }
    }
}
