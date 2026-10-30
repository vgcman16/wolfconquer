using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;

namespace WolfConquer.Tests;

// Corrected-only owned controls. No old unsafe capacity/oversize baseline is executed.
public sealed unsafe partial class LegacyNativeArenaMatchPacketTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task IncompleteRankingQueryPreservesBothOutputsAndItsOriginalCursor(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = new Packet(Math.Max(1, count));
        byte[] initialized = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        Marshal.Copy(initialized, 0, (nint)packet.Memory, initialized.Length);
        packet.Size = count; // Actual chosen input extent; any spare allocation is not input.
        var rank = (LegacyNativeMsgArenaRanking.RankTyp)91; ushort page = 92;
        Assert.Throws<EndOfStreamException>(() => packet.GetArenaRanking(out rank, out page));
        Assert.Equal((LegacyNativeMsgArenaRanking.RankTyp)91, rank); Assert.Equal((ushort)92, page);
        Assert.Equal(0, packet.Position); Assert.Equal(count, packet.Size); Assert.Equal(initialized, ArenaAllocation(packet));
    });

    [Fact]
    public Task IndependentRankingQueryGoldenConsumesAnExactFourByteAllocation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = new Packet(4);
        byte[] input = Convert.FromHexString("FFFF31D4");
        Marshal.Copy(input, 0, (nint)packet.Memory, input.Length); packet.Size = input.Length;
        packet.GetArenaRanking(out var rank, out var page);
        Assert.Equal(ushort.MaxValue, (ushort)rank); Assert.Equal((ushort)54321, page);
        Assert.Equal(4, packet.Position); Assert.Equal(4, packet.Size); Assert.Equal(input, ArenaAllocation(packet));
    });

    [Fact]
    public Task IncrementalRankingHeaderUsesItsExplicitDestinationWhilePreservingOldSize() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(20);
        packet.ArenaRankingCreate(LegacyNativeMsgArenaRanking.RankTyp.History, 2, 7,
            LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
        Assert.Equal(20, packet.Position); Assert.Equal(13, packet.Size);
        byte[] expected = RankingHeader(1, 2, 7, 0);
        Assert.Equal(expected[4..], ArenaAllocation(packet)[4..]);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, ArenaAllocation(packet)[..4]);
        packet.ArenaRankingFinalize();
        Assert.Equal(expected, ArenaAllocation(packet)); Assert.Equal(20, packet.Size); Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task IncompleteRankingHeaderCapacityCannotReplaceAnyOldTargetBytes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(19);
        byte[] before = ArenaAllocation(packet);
        Assert.Throws<InvalidOperationException>(() => packet.ArenaRankingCreate(
            LegacyNativeMsgArenaRanking.RankTyp.Today, 1, 7, LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList));
        Assert.Equal(before, ArenaAllocation(packet)); Assert.Equal(18, packet.Position); Assert.Equal(13, packet.Size);
    });

    [Theory]
    [InlineData(75, false)] [InlineData(76, true)]
    public Task IncrementalRankingRowUsesItsWholeFiftySixByteSegmentOrPreservesTheHeader(int capacity, bool fits) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(capacity);
        packet.ArenaRankingCreate(LegacyNativeMsgArenaRanking.RankTyp.History, 2, 1000,
            LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
        byte[] before = ArenaAllocation(packet);
        if (!fits)
        {
            Assert.Throws<InvalidOperationException>(() => packet.AddItemArenaRanking(9, "Literal", 6004, 12345, 16004, 130, 33942209, 1003));
            Assert.Equal(before, ArenaAllocation(packet)); Assert.Equal(20, packet.Position); Assert.Equal(13, packet.Size);
            return;
        }
        packet.AddItemArenaRanking(9, "Literal", 6004, 12345, 16004, 130, 33942209, 1003);
        Assert.Equal(76, packet.Position); Assert.Equal(13, packet.Size);
        packet.ArenaRankingFinalize();
        byte[] expected = RankingHeader(1, 2, 1000, 1);
        RankingItem(expected, 20, 9, "Literal", 6004, 12345, 16004, 130, 33942209, 1003);
        Assert.Equal(expected, ArenaAllocation(packet)); Assert.Equal(76, packet.Size); Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task NullRankingAppendNameCannotPublishItsRankPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(76);
        packet.ArenaRankingCreate(LegacyNativeMsgArenaRanking.RankTyp.Today, 1, 1,
            LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList);
        byte[] before = ArenaAllocation(packet);
        Assert.Throws<ArgumentNullException>(() => packet.AddItemArenaRanking(9, null!, 0, 1, 2, 3, 4, 5));
        Assert.Equal(before, ArenaAllocation(packet)); Assert.Equal(20, packet.Position); Assert.Equal(13, packet.Size);
    });

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)]
    public Task LaterInvalidRankingNameCannotPublishAnEarlierValidResponseRow(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = Row(1); var second = Row(2); second.Name = null!;
        LegacyNativeArenaRankings.Top1000Today = [first, second]; LegacyNativeArenaRankings.Top1000 = [first, second];
        RankingQuery(scope.Packet, rank, 1); byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet));
        Assert.Equal(before, ArenaAllocation(scope.Packet)); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue); Assert.Equal("Rank1", first.Name); Assert.Null(second.Name);
    });

    [Fact]
    public Task WholeRankingResponseCapacityFailurePreservesTheConsumedQueryAndAllBytes() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(75);
        LegacyNativeArenaRankings.Top1000Today = [Row(1)];
        RankingQuery(packet, 0, 1); packet.Size = 8;
        byte[] before = ArenaAllocation(packet);
        Assert.Throws<InvalidOperationException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, packet));
        Assert.Equal(before, ArenaAllocation(packet)); Assert.Equal(8, packet.Position); Assert.Equal(8, packet.Size); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(95, false)] [InlineData(96, true)]
    public Task ScoreUsesTheWholeAllocationAndPublishesOnlyACompleteFrame(int capacity, bool fits) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(capacity);
        byte[] before = ArenaAllocation(packet);
        if (!fits)
        {
            Assert.Throws<InvalidOperationException>(() => packet.ArenaMatchScoreCreate(1, "A", 2, 3, "B", 4));
            Assert.Equal(before, ArenaAllocation(packet)); Assert.Equal(capacity - 1, packet.Position); Assert.Equal(13, packet.Size);
            return;
        }
        packet.ArenaMatchScoreCreate(1, "A", 2, 3, "B", 4);
        Assert.Equal(ScoreBytes(1, "A", 2, 3, "B", 4), ArenaAllocation(packet));
        Assert.Equal(96, packet.Size); Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task NullSecondScoreNamePreservesTheEntireOldFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => scope.Packet.ArenaMatchScoreCreate(1, "A", 2, 3, null!, 4));
        Assert.Equal(before, ArenaAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData("header")] [InlineData("row")] [InlineData("score")]
    public Task DisposedArenaOutputTargetRefusesWithoutChangingRetainedMetadata(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedArenaPacket(96);
        nuint cursor = (nuint)packet.stream; packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => BuildArena(packet, operation));
        Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(13, packet.Size); Assert.Equal((nuint)0, (nuint)packet.Memory);
    });

    [Theory]
    [InlineData("header")] [InlineData("row")] [InlineData("score")]
    public Task NullArenaOutputTargetRefusesExplicitly(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Assert.Throws<ArgumentNullException>(() => BuildArena(null!, operation));
        Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    private static void BuildArena(Packet packet, string operation)
    {
        switch (operation)
        {
            case "header": packet.ArenaRankingCreate(LegacyNativeMsgArenaRanking.RankTyp.Today, 1, 1, LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList); break;
            case "row": packet.AddItemArenaRanking(1, "A", 0, 2, 3, 4, 5, 6); break;
            case "score": packet.ArenaMatchScoreCreate(1, "A", 2, 3, "B", 4); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static Packet InitializedArenaPacket(int capacity)
    {
        var packet = new Packet(capacity);
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(), 0, (nint)packet.Memory, capacity);
        packet.Size = 13; packet.Seek(capacity - 1);
        return packet;
    }

    private static byte[] ArenaAllocation(Packet packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }
}
