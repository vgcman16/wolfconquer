using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientProficiencyPersistenceTests
{
    [Fact]
    public void RootedFileLeafReparseRefusesLoadAndSaveWithoutTouchingTarget()
    {
        using var files = new OwnedProfs();
        var target = Path.Combine(files.DirectoryPath, "owned-other.bin");
        byte[] before = Image(1, Row(77, 4));
        File.WriteAllBytes(target, before);
        bool linked = false;
        try
        {
            try { File.CreateSymbolicLink(files.FilePath, target); linked = true; }
            catch (UnauthorizedAccessException)
            { throw Xunit.Sdk.SkipException.ForSkip("Windows symbolic-link privilege is unavailable; no settings changed."); }
            catch (IOException error) when ((error.HResult & 0xffff) == 1314)
            { throw Xunit.Sdk.SkipException.ForSkip("Windows symbolic-link privilege is unavailable; no settings changed."); }
            Assert.True((File.GetAttributes(files.FilePath) & FileAttributes.ReparsePoint) != 0);
            var client = Client();
            Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.LoadClientProfs(client));
            Assert.Throws<IOException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(client));
            Assert.Empty(client.MyProfs!.ClientProf);
            Assert.Equal(before, File.ReadAllBytes(target));
        }
        finally
        {
            if (linked) File.Delete(files.FilePath);
            File.Delete(target);
        }
    }

    [Fact]
    public void RootChangeInsideRealMapComparerRefusesBeforeSave()
    {
        using var files = new OwnedProfs();
        var before = Image(1, Row(77, 4));
        files.Seed(before);
        var oldRoot = LegacyNativeServerConfig.DbLocation;
        var alternate = Path.Combine(Path.GetDirectoryName(files.DirectoryPath)!, "other-owned-root");
        Directory.CreateDirectory(alternate);
        try
        {
            var client = Client();
            var comparer = new PathChangingComparer(() => LegacyNativeServerConfig.DbLocation = alternate);
            var map = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>(comparer);
            var row = new LegacyNativeMsgProficiency { ID = 77, Level = 4 };
            Assert.True(map.TryAdd(77, row));
            client.MyProfs!.ClientProf = map;
            comparer.Armed = true;
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientProfs(client));
            Assert.True(comparer.Triggered);
            Assert.Same(map, client.MyProfs.ClientProf); Assert.Same(row, map[77]);
            Assert.Equal(before, File.ReadAllBytes(files.FilePath));
            Assert.Empty(Directory.GetFileSystemEntries(alternate));
        }
        finally
        {
            LegacyNativeServerConfig.DbLocation = oldRoot;
            Directory.Delete(alternate, false);
        }
    }

    private sealed class PathChangingComparer(Action change) : IEqualityComparer<uint>
    {
        internal bool Armed, Triggered;
        public bool Equals(uint x, uint y) => x == y;
        public int GetHashCode(uint value)
        {
            if (Armed) { Armed = false; Triggered = true; change(); }
            return value.GetHashCode();
        }
    }
}
