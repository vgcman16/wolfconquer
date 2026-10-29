using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRankItemsPersistenceTests
{
    [Fact]
    public void MissingFileStillCreatesAllElevenRanksBeforeCheckingExistence()
    {
        using var fixture = new Files();
        LegacyNativeRankItems.LoadAllItems();
        Assert.Equal(Enumerable.Range(1, 11).Select(value => (uint)value), LegacyNativeRankItems.RankPoll.Keys);
        Assert.All(LegacyNativeRankItems.RankPoll.Values, rank => Assert.Empty(rank.Items));
        Assert.False(File.Exists(fixture.Path));
        Assert.Throws<ArgumentException>(() => LegacyNativeRankItems.LoadAllItems());
    }

    [Fact]
    public void GenuineBinaryFileRoundTripWritesElevenSectionsAndBothNativeRecordLayouts()
    {
        using var fixture = new Files();
        LegacyNativeRankItems.CreateRanks();
        var item = new LegacyNativeGameItemRuntime { Uid = 0x12345678, ITEM_ID = 111003,
            Position = 1, Plus = 7, ItemPoints = 12345, PerfectionLevel = 54,
            PerfectionProgress = 9876, OwnerUID = 77, OwnerName = "Wolf", Signature = "Owned" };
        LegacyNativeRankItems.RankPoll[1].AddItem(item);
        var other = new LegacyNativeGameItemRuntime { Uid = 22, ITEM_ID = 202003,
            Position = 11, Plus = 3, ItemPoints = 9, PerfectionLevel = 5,
            PerfectionProgress = 8, OwnerUID = 2, OwnerName = "A", Signature = "B" };
        LegacyNativeRankItems.RankPoll[11].AddItem(other);
        LegacyNativeRankItems.SaveRanks();
        var bytes = File.ReadAllBytes(fixture.Path);
        Assert.Equal(4 + 11 * 4 + 2 * (184 + 80), bytes.Length);
        Assert.Equal(11, BitConverter.ToInt32(bytes, 0));
        Assert.Equal(1, BitConverter.ToInt32(bytes, 4));
        Assert.Equal(item.Uid, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(item.Uid, BitConverter.ToUInt32(bytes, 8 + 184));
        Assert.Equal(54u, BitConverter.ToUInt32(bytes, 8 + 188));

        LegacyNativeRankItems.RankPoll = new();
        LegacyNativeRankItems.LoadAllItems();
        var loaded = Assert.Single(LegacyNativeRankItems.RankPoll[1].Items).Value;
        Assert.NotSame(item, loaded);
        Assert.Equal(item.Uid, loaded.Uid);
        Assert.Equal(item.ITEM_ID, loaded.ITEM_ID);
        Assert.Equal(item.Plus, loaded.Plus);
        Assert.Equal(item.ItemPoints, loaded.ItemPoints);
        Assert.Equal(item.PerfectionLevel, loaded.PerfectionLevel);
        Assert.Equal(item.PerfectionProgress, loaded.PerfectionProgress);
        Assert.Equal(item.OwnerUID, loaded.OwnerUID);
        Assert.Equal(item.OwnerName, loaded.OwnerName);
        Assert.Equal(item.Signature, loaded.Signature);
        Assert.Equal((ushort)1, loaded.PerfectionRank);
        Assert.Equal(22u, Assert.Single(LegacyNativeRankItems.RankPoll[11].Items).Value.Uid);
    }

    [Fact]
    public void SavePersistsTheCurrentReplacementRatherThanAnEscapedOldValueSnapshot()
    {
        using var fixture = new Files();
        LegacyNativeRankItems.CreateRanks();
        var rank = LegacyNativeRankItems.RankPoll[1];
        var old = new LegacyNativeGameItemRuntime { Uid = 1, ITEM_ID = 111003, Plus = 2,
            Position = 0, ItemPoints = 10, OwnerName = "old" };
        rank.AddItem(old);
        var cached = rank.Items.GetValues();
        var replacement = new LegacyNativeGameItemRuntime { Uid = 1, ITEM_ID = 111003,
            Plus = 12, ItemPoints = 0, OwnerName = "new" };
        rank.AddItem(replacement);
        Assert.Same(replacement, rank.Items[1]);
        Assert.Same(old, Assert.Single(cached)); Assert.True(rank.Items.Update);
        long revision = rank.Items.Revision;
        LegacyNativeRankItems.SaveRanks();
        Assert.Equal(revision, rank.Items.Revision); Assert.False(rank.Items.Update);
        Assert.Same(replacement, Assert.Single(rank.Items.GetValues()));
        LegacyNativeRankItems.RankPoll = new();
        LegacyNativeRankItems.LoadAllItems();
        var loaded = Assert.Single(LegacyNativeRankItems.RankPoll[1].Items).Value;
        Assert.Equal((byte)12, loaded.Plus);
        Assert.Equal(0u, loaded.ItemPoints);
        Assert.Equal("new", loaded.OwnerName);
    }

    private sealed class Files : IDisposable
    {
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly Dictionary<uint, LegacyNativeRankItems.Rank> priorRanks = LegacyNativeRankItems.RankPoll;
        private readonly string temp = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        private readonly string root;
        internal string Path => root + "\\PerfectionRanking.bin";

        internal Files()
        {
            root = System.IO.Path.GetFullPath(System.IO.Path.Combine(temp, "wolf-native-rank-" + Guid.NewGuid().ToString("N")));
            if (Directory.Exists(root) || File.Exists(root))
                throw new IOException("The owned GUID fixture path already exists.");
            Directory.CreateDirectory(root);
            LegacyNativeServerConfig.DbLocation = root;
            LegacyNativeRankItems.RankPoll = new();
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = priorLocation;
            LegacyNativeRankItems.RankPoll = priorRanks;
            var parent = System.IO.Path.GetDirectoryName(root);
            if (!string.Equals(parent?.TrimEnd('\\', '/'), temp.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                || !System.IO.Path.GetFileName(root).StartsWith("wolf-native-rank-", StringComparison.Ordinal)
                || System.IO.Path.GetFileName(root).Length != "wolf-native-rank-".Length + 32)
                throw new InvalidOperationException("Refusing cleanup outside the owned GUID rank fixture.");
            Directory.Delete(root, true);
        }
    }
}
