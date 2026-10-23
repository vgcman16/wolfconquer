using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeViewTableDependencyTests
{
    [Fact]
    public Task PokerLoadUsesRealConstructorsCurrencyTableTypeAndSkipsDuplicateTailParsing() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.File("PokerTables.txt", "1 10 20 7578087 40 1 1 50", "2 11 21 999 41 0 0 51", "1");
        LegacyNativePokerDatabase.Load();
        Assert.Equal(2, LegacyNativePokerDatabase.Tables!.Count);
        var first = LegacyNativePokerDatabase.Tables[1];
        Assert.Equal((ushort)10, first.X); Assert.Equal((ushort)20, first.Y);
        Assert.Equal(7578087u, first.Mesh); Assert.Equal(40u, first.Number);
        Assert.True(first.UnLimited); Assert.True(first.IsCPs); Assert.Equal((ushort)1860, first.MapId);
        Assert.Equal(50u, first.MinBet); Assert.Equal(LegacyNativePokerFlags.TableType.ShowHand, first.TableType);
        Assert.Empty(first.Players); Assert.Empty(first.Watchers); Assert.Null(first.OnScreen); Assert.Null(first.Cards);
        var second = LegacyNativePokerDatabase.Tables[2];
        Assert.Equal((ushort)1858, second.MapId); Assert.False(second.UnLimited); Assert.False(second.IsCPs);
        Assert.Equal(LegacyNativePokerFlags.TableType.TexasHoldem, second.TableType);
    });

    [Fact]
    public Task PokerLoadReplacesGlobalBeforeMissingFileAndPreservesOldAlias() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var old = new Dictionary<uint, LegacyNativePokerTableRuntime> { [1] = new(1) };
        LegacyNativePokerDatabase.Tables = old;
        Assert.Throws<FileNotFoundException>(() => LegacyNativePokerDatabase.Load());
        Assert.NotSame(old, LegacyNativePokerDatabase.Tables); Assert.Empty(LegacyNativePokerDatabase.Tables!);
        Assert.Single(old); Assert.Equal(1u, old[1].Id);
    });

    [Theory]
    [InlineData("", typeof(FormatException))]
    [InlineData("3  1 2 3 0 0 4", typeof(FormatException))]
    [InlineData("3 1 2", typeof(IndexOutOfRangeException))]
    [InlineData("3 1 2 3 4 256 0 5", typeof(OverflowException))]
    public Task PokerLoadRetainsCommittedPrefixWhenNextWholeRowFaults(string invalid, Type error) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.File("PokerTables.txt", "1 10 20 50 1 0 0 5", invalid);
        Assert.Throws(error, () => LegacyNativePokerDatabase.Load());
        Assert.Single(LegacyNativePokerDatabase.Tables!);
        Assert.True(LegacyNativePokerDatabase.Tables!.ContainsKey(1));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task RouletteLoadGenuinelyIgnoresZeroOrOneLineWithoutClearingExistingPool(int count) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = s.Table(99); LegacyNativeRoulettes.RoulettesPoll[99] = table;
        s.File("Roulettes.txt", Enumerable.Repeat("1,2,1,3053,4,5,6", count).ToArray());
        LegacyNativeRoulettes.Load();
        Assert.Single(LegacyNativeRoulettes.RoulettesPoll); Assert.Same(table, LegacyNativeRoulettes.RoulettesPoll[99]);
    });

    [Fact]
    public Task RouletteLoadSkipsExactCommentsKeepsOldRowsAndUsesAllNativeFieldWidths() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var old = s.Table(7); LegacyNativeRoulettes.RoulettesPoll[7] = old;
        s.File("Roulettes.txt", ";header", "7,99,2,3053,1,2,3", "8,12,2,65000,100,101,65535");
        LegacyNativeRoulettes.Load();
        Assert.Same(old, LegacyNativeRoulettes.RoulettesPoll[7]);
        var actual = LegacyNativeRoulettes.RoulettesPoll[8];
        Assert.Equal(12u, actual.SpawnPacket.TableNumber);
        Assert.Equal(LegacyNativeMsgRouletteTable.TableType.ConquerPoints, actual.SpawnPacket.MoneyType);
        Assert.Equal((ushort)100, actual.SpawnPacket.X); Assert.Equal((ushort)101, actual.SpawnPacket.Y);
        Assert.Equal(ushort.MaxValue, actual.SpawnPacket.Mesh);
        Assert.Empty(actual.RegistredPlayers); Assert.Empty(actual.ClientsWatch); Assert.Equal((byte)1, actual.LuckyNumber);
    });

    [Fact]
    public Task RouletteLoadDoesNotTrimOrSkipBlankRowsAndParserDefaultsMalformedTokens() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.File("Roulettes.txt", ";header", "", "9,bad,not-a-type,3053,bad,5,65536");
        LegacyNativeRoulettes.Load();
        Assert.Equal(2, LegacyNativeRoulettes.RoulettesPoll.Count);
        Assert.Equal(0u, LegacyNativeRoulettes.RoulettesPoll[0].SpawnPacket.UID);
        var item = LegacyNativeRoulettes.RoulettesPoll[9].SpawnPacket;
        Assert.Equal(0u, item.TableNumber); Assert.Equal(0u, (uint)item.MoneyType);
        Assert.Equal((ushort)0, item.X); Assert.Equal((ushort)5, item.Y); Assert.Equal((ushort)0, item.Mesh);
    });

    [Fact]
    public Task RouletteMissingFileDoesNotReplacePool() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var alias = LegacyNativeRoulettes.RoulettesPoll;
        alias[1] = s.Table(1);
        Assert.Throws<FileNotFoundException>(() => LegacyNativeRoulettes.Load());
        Assert.Same(alias, LegacyNativeRoulettes.RoulettesPoll); Assert.Single(alias);
    });
}
