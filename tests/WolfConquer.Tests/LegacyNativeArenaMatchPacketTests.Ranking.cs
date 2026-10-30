using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeArenaMatchPacketTests
{
    [Fact]
    public Task EntireRankingCodecWritesExactNativeHeaderItemAndFinalizerWithoutChangingMarkerTen() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var packet = scope.Packet;
        Assert.Same(packet, packet.ArenaRankingCreate(LegacyNativeMsgArenaRanking.RankTyp.History, 2, 1000,
            LegacyNativeArenaRuntime.ArenaIDs.ShowPlayerRankList));
        Assert.Equal(20, packet.Position); Assert.Equal(321, packet.Size);
        Assert.Same(packet, packet.AddItemArenaRanking(9, "Literal", 6004, 12345, 16004, 130, 33942209, 1003));
        Assert.Equal(76, packet.Position); Assert.Same(packet, packet.ArenaRankingFinalize());
        byte[] expected = RankingHeader(1, 2, 1000, 1);
        RankingItem(expected, 20, 9, "Literal", 6004, 12345, 16004, 130, 33942209, 1003);
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(packet)); Assert.Equal(4, packet.Position);
    });

    [Fact]
    public Task WholeRankingReaderConsumesOnlyTwoUShortsAndAcceptsUnknownRankAndPage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Packet.InitWriter();
        scope.Packet.Write(ushort.MaxValue); scope.Packet.Write((ushort)54321); scope.Packet.Write(99u); scope.Packet.Seek(4);
        scope.Packet.GetArenaRanking(out var rank, out var page);
        Assert.Equal(ushort.MaxValue, (ushort)rank); Assert.Equal((ushort)54321, page);
        Assert.Equal(8, scope.Packet.Position); Assert.Equal(99u, scope.Packet.ReadUInt32());
        Assert.Equal(321, scope.Packet.Size); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task FullPrivateRankingReceiverUsesItsWholeTodayOrHistoryBranch(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(1);
        LegacyNativeArenaRankings.Top1000Today[0] = row; LegacyNativeArenaRankings.Top1000[0] = row;
        RankingQuery(scope.Packet, rank, 1);
        InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        Assert.Equal(1, Marshal.SizeOf<LegacyNativeMsgArenaRanking>());
        Assert.Equal(LayoutKind.Sequential, typeof(LegacyNativeMsgArenaRanking).StructLayoutAttribute!.Value);
        byte[] expected = RankingHeader(rank, 1, 1, 1);
        RankingItem(expected, 20, 1, "Rank1", rank == 0 ? (ushort)0 : (ushort)6004,
            rank == 0 ? 101u : 201u, 16004, 130, rank == 0 ? 1u : 33942209u, 1003);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(1u, row.Info.TodayRank);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task FullRankingReceiverPagesEightRowsAndUsesGlobalPositionNotTheStoredTodayRank(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var rows = Enumerable.Range(1, 9).Select(x => Row((uint)x)).ToArray();
        foreach (var row in rows) row.Info.TodayRank = 777;
        LegacyNativeArenaRankings.Top1000Today = rows; LegacyNativeArenaRankings.Top1000 = rows;
        RankingQuery(scope.Packet, rank, 2);
        InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        byte[] expected = RankingHeader(rank, 2, 9, 1);
        RankingItem(expected, 20, 9, "Rank9", rank == 0 ? (ushort)0 : (ushort)6004,
            rank == 0 ? 109u : 209u, 16004, 130, rank == 0 ? 1u : 33942209u, 1003);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.All(rows, row => Assert.Equal(777u, row.Info.TodayRank));
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task RankAtOneThousandIsOmittedEvenForHistoryWhileCountStillIncludesIt(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var include = Row(1); var omit = Row(2);
        include.Info.TodayRank = 0; omit.Info.TodayRank = 1000;
        LegacyNativeArenaRankings.Top1000Today[0] = include; LegacyNativeArenaRankings.Top1000Today[1] = omit;
        LegacyNativeArenaRankings.Top1000[0] = include; LegacyNativeArenaRankings.Top1000[1] = omit;
        RankingQuery(scope.Packet, rank, 1);
        InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        byte[] expected = RankingHeader(rank, 1, 2, 1);
        RankingItem(expected, 20, 1, "Rank1", rank == 0 ? (ushort)0 : (ushort)6004,
            rank == 0 ? 101u : 201u, 16004, 130, rank == 0 ? 1u : 33942209u, 1003);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.Equal(1000u, omit.Info.TodayRank);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task RankingFiltersNullHolesButKeepsDuplicatePhysicalRowsInTheirArrayOrder(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(1);
        LegacyNativeArenaRankings.Top1000Today[2] = row; LegacyNativeArenaRankings.Top1000Today[9] = row;
        LegacyNativeArenaRankings.Top1000[2] = row; LegacyNativeArenaRankings.Top1000[9] = row;
        RankingQuery(scope.Packet, rank, 1); InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        byte[] expected = RankingHeader(rank, 1, 2, 2);
        for (int index = 0; index < 2; index++)
            RankingItem(expected, 20 + 56 * index, (ushort)(index + 1), "Rank1", rank == 0 ? (ushort)0 : (ushort)6004,
                rank == 0 ? 101u : 201u, 16004, 130, rank == 0 ? 1u : 33942209u, 1003);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.Equal(1u, row.Info.TodayRank);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task FullRankingReceiverReturnsHeaderOnlyForALargePageWithoutClampingIt(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(1);
        LegacyNativeArenaRankings.Top1000Today[0] = row; LegacyNativeArenaRankings.Top1000[0] = row;
        RankingQuery(scope.Packet, rank, ushort.MaxValue); InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        Assert.Equal(RankingHeader(rank, ushort.MaxValue, 1, 0), Assert.Single(scope.Queue));
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task PageZeroRefusesWithoutPublishingAResponseHeader(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); RankingQuery(scope.Packet, rank, 0);
        byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<ArgumentOutOfRangeException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, ArenaAllocation(scope.Packet));
    });

    [Fact]
    public Task UnknownRankingTypeOnlyConsumesTheQueryWithoutResponseOrFinalization() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); RankingQuery(scope.Packet, ushort.MaxValue, 1);
        InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet);
        Assert.Empty(scope.Queue); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(ushort.MaxValue, BitConverter.ToUInt16(NativeSpellTestFixture.Bytes(scope.Packet), 4));
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task NullInfoRankingRefusesBeforeAnyResponsePublication(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(1); row.Info = null!;
        LegacyNativeArenaRankings.Top1000Today[0] = row; LegacyNativeArenaRankings.Top1000[0] = row;
        RankingQuery(scope.Packet, rank, 1);
        byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, ArenaAllocation(scope.Packet));
        Assert.Null(row.Info);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task NullNameRankingRefusesWithoutPublishingEvenAnItemRank(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(1); row.Name = null!;
        LegacyNativeArenaRankings.Top1000Today[0] = row; LegacyNativeArenaRankings.Top1000[0] = row;
        RankingQuery(scope.Packet, rank, 1);
        byte[] before = ArenaAllocation(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Equal(before, ArenaAllocation(scope.Packet)); Assert.Null(row.Name);
    });

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    public Task NullSelectedRankingArrayFaultsAfterReadingTheRequestButBeforeWriterInitialization(ushort rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeArenaRankings.Top1000Today = null!; LegacyNativeArenaRankings.Top1000 = null!;
        RankingQuery(scope.Packet, rank, 1);
        Assert.Throws<ArgumentNullException>(() => InvokePrivate<LegacyNativeMsgArenaRanking>(scope.Client, scope.Packet));
        Assert.Empty(scope.Queue); Assert.Equal(8, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
    });
}
