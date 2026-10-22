using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    internal static byte[] Row(ushort id, ushort level = 0, int experience = 0,
        byte previous = 0, byte soul = 0, byte used = 0, byte padding = 0)
    {
        var bytes = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, id);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), level);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), experience);
        bytes[8] = previous; bytes[9] = soul; bytes[10] = used; bytes[11] = padding;
        return bytes;
    }

    internal static byte[] Image(int count, params byte[][] rows) =>
        BitConverter.GetBytes(count).Concat(rows.SelectMany(value => value)).ToArray();

    internal static byte[] Bytes<T>(ref T row) where T : struct =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref row, 1)).ToArray();

    private static LegacyNativeGameClientRuntime Client(bool spells = true, uint uid = 1293600)
    {
        var client = new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false };
        client.Player.UID = uid;
        if (spells) client.MySpells = new LegacyNativeSpellStorage(client);
        return client;
    }

    private static void SetClass(LegacyNativeGameClientRuntime client, uint value) =>
        typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client.Player, value);

    private static void AssertClosed(string path)
    {
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    private static byte[] ReadWhileOpen(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var data = new MemoryStream();
        stream.CopyTo(data);
        return data.ToArray();
    }

    internal sealed class OwnedSpells : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly LegacyNativeMagicType priorMagic = LegacyNativePoolMagic.Magic;
        private readonly LegacyNativeItemTypeRuneMetadata? priorItems = LegacyNativePoolItems.ItemsBase;
        private readonly string root;
        private readonly List<LegacyNativeItemRecordFile> files = new();
        internal string DirectoryPath { get; }
        internal string FilePath { get; }
        internal LegacyNativeMagicType Magic { get; } = new();
        internal LegacyNativeItemTypeRuneMetadata Items { get; } = new();

        internal OwnedSpells(bool createDirectory = true)
        {
            Assert.True(OperatingSystem.IsWindows(), "Actual native spell-file I/O requires Windows Kernel32.");
            root = Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), "native-client-spell-" + Guid.NewGuid().ToString("N"));
            DirectoryPath = Path.Combine(root, "PlayersSpells");
            FilePath = Path.Combine(DirectoryPath, "1293600.bin");
            Guard();
            Assert.False(Directory.Exists(root));
            Directory.CreateDirectory(root);
            if (createDirectory) Directory.CreateDirectory(DirectoryPath);
            LegacyNativeServerConfig.DbLocation = root;
            LegacyNativePoolMagic.Magic = Magic;
            LegacyNativePoolItems.ItemsBase = Items;
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
            LegacyNativeServerConfig.DbLocation = priorLocation;
            LegacyNativePoolMagic.Magic = priorMagic;
            LegacyNativePoolItems.ItemsBase = priorItems;
            // Every phase-fault case retains its exact real file owner here.
            // No finalizer/GC-based handle recovery or production finally exists.
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
            var parent = LegacyNativeDbReadOwnedFiles.ArtifactsRoot();
            Assert.Equal(parent, Path.GetDirectoryName(Path.GetFullPath(root)));
            Assert.Matches("^native-client-spell-[0-9a-f]{32}$", Path.GetFileName(root));
            Assert.Equal(root, Path.GetDirectoryName(DirectoryPath));
            Assert.Equal(DirectoryPath, Path.GetDirectoryName(FilePath));
            Assert.Equal("1293600.bin", Path.GetFileName(FilePath));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(FilePath);
        }
    }

    private sealed class CapturedConsole : IDisposable
    {
        private readonly TextWriter prior = Console.Out;
        private readonly StringWriter writer = new();
        internal string Text => writer.ToString();
        internal CapturedConsole() => Console.SetOut(writer);
        public void Dispose() { Console.SetOut(prior); writer.Dispose(); }
    }
}
