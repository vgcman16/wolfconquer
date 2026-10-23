using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PlayerQuestSnapshotStructureTests
{
    [Fact]
    public void ValidLegacyBytesPreserveUnsortedRowsStatusesIntentionsAndPhysicalAcceptedCache()
    {
        var bytes = ValidImage();
        var snapshot = PlayerQuestSnapshotLoader.Parse(bytes, "legacy.bin");

        Assert.Equal(new uint[] { 35_024, 2_001, 5 }, snapshot.Quests.Select(row => row.Uid));
        Assert.Equal(new[] { PlayerQuestStatus.Accepted, PlayerQuestStatus.Finished, PlayerQuestStatus.Available },
            snapshot.Quests.Select(row => row.Status));
        Assert.Equal(new uint[] { 123_456, 12, 99 }, snapshot.Quests.Select(row => row.Time));
        Assert.Equal(new uint[] { 7, 9 }, snapshot.Quests[0].Intentions);
        Assert.Equal(new uint[] { 3 }, snapshot.Quests[1].Intentions);
        Assert.Empty(snapshot.Quests[2].Intentions);
        Assert.Same(snapshot.Quests[0], Assert.Single(snapshot.AcceptedQuests));
        Assert.Equal(bytes, PlayerQuestSnapshotLoader.Serialize(snapshot));
    }

    [Fact]
    public void EmptyImageAndZeroIntentionsRetainLegacyShape()
    {
        var empty = PlayerQuestSnapshotLoader.Parse(HeaderOnly(0));
        Assert.Empty(empty.Quests);
        Assert.Empty(empty.AcceptedQuests);
        Assert.Equal(HeaderOnly(0), PlayerQuestSnapshotLoader.Serialize(empty));

        var zero = PlayerQuestSnapshotLoader.Parse(OneHeader(0));
        Assert.Empty(Assert.Single(zero.Quests).Intentions);
        Assert.Same(zero.Quests[0], Assert.Single(zero.AcceptedQuests));
        Assert.Equal(OneHeader(0), PlayerQuestSnapshotLoader.Serialize(zero));
    }

    [Fact]
    public void AValidImageAboveTheVictoryComponentLimitIsNotGivenANewGlobalCap()
    {
        // The general format has Int32 counts/offsets, not Victory's separate
        // 16MiB transaction bound. About 48MiB of live byte/uint arrays, no OOM.
        const int intentions = 4_194_304;
        var bytes = new byte[sizeof(int) + PlayerQuestSnapshotLoader.LegacyQuestHeaderSize +
            intentions * sizeof(uint)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3_944);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), uint.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), intentions);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - sizeof(uint)), uint.MaxValue);

        var snapshot = PlayerQuestSnapshotLoader.Parse(bytes, "large-valid.bin");
        var row = Assert.Single(snapshot.Quests);
        Assert.Equal(intentions, row.Intentions.Count);
        Assert.Equal(7U, row.Intentions[0]);
        Assert.Equal(uint.MaxValue, row.Intentions[^1]);
        Assert.Equal(uint.MaxValue, row.Time);
        Assert.Same(row, Assert.Single(snapshot.AcceptedQuests));
        Assert.Equal(bytes, PlayerQuestSnapshotLoader.Serialize(snapshot));
    }
}
