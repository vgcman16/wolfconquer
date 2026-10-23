using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeRoulettes.RouletteTable;
using Color = WolfConquer.Core.World.LegacyNativeMsgRouletteOpenGui.Color;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeViewTableDependencyTests
{
    [Fact]
    public void RouletteWholeConstructorHasRealIndependentCollectionsClockAndSourceDefaults()
    {
        DateTime before = DateTime.Now.AddSeconds(30);
        var table = new Table();
        DateTime after = DateTime.Now.AddSeconds(30);
        Assert.InRange(table.TimerStamp, before, after);
        Assert.Equal(default, table.StampRound); Assert.Equal((byte)1, table.LuckyNumber);
        Assert.NotNull(table.Rand); Assert.Empty(table.RegistredPlayers); Assert.Empty(table.ClientsWatch);
        Assert.NotNull(table.SpawnPacket); Assert.Equal(0u, table.SpawnPacket.UID);
        Assert.Equal(18, table.Red.Count); Assert.Equal(18, table.Black.Count);
        Assert.Empty(table.Red.Intersect(table.Black));
        Assert.Equal(Enumerable.Range(1, 36).Select(x => (byte)x), table.Red.Concat(table.Black).OrderBy(x => x));
    }

    [Theory]
    [InlineData(0, false, false, false, false, false, false, false)]
    [InlineData(1, true, false, true, false, true, false, false)]
    [InlineData(12, false, true, true, false, true, false, false)]
    [InlineData(13, true, false, true, false, false, true, false)]
    [InlineData(18, false, true, true, false, false, true, false)]
    [InlineData(19, true, false, false, true, false, true, false)]
    [InlineData(24, false, true, false, true, false, true, false)]
    [InlineData(25, true, false, false, true, false, false, true)]
    [InlineData(36, false, true, false, true, false, false, true)]
    [InlineData(37, false, false, false, false, false, false, false)]
    [InlineData(255, false, false, false, false, false, false, false)]
    public void WholeNumberPredicatesPreserveEveryNativeBoundary(byte number, bool odd, bool even, bool small, bool big, bool front, bool middle, bool back)
    {
        var info = new LegacyNativeRoulettes.NumberInformation();
        Assert.Equal(odd, info.IsOdd(number)); Assert.Equal(even, info.IsEven(number));
        Assert.Equal(small, info.IsSmall(number)); Assert.Equal(big, info.IsBig(number));
        Assert.Equal(front, info.IsFront(number)); Assert.Equal(middle, info.IsMiddle(number)); Assert.Equal(back, info.IsBack(number));
    }

    [Theory]
    [InlineData(151, 1, 2)]
    [InlineData(152, 2, 1)]
    [InlineData(112, 1, 2)]
    [InlineData(111, 2, 1)]
    [InlineData(141, 18, 19)]
    [InlineData(142, 19, 18)]
    [InlineData(121, 12, 13)]
    [InlineData(122, 13, 25)]
    [InlineData(123, 25, 24)]
    [InlineData(131, 34, 35)]
    [InlineData(132, 35, 36)]
    [InlineData(133, 36, 35)]
    public void CompleteLineSwitchUsesMutableRealListsAndNativeLuckyNumber(byte extra, byte winning, byte losing)
    {
        var table = new Table();
        var member = new Table.Member();
        member.MyLuckExtra[extra] = new() { Number = extra, BetPrice = 1 };
        table.LuckyNumber = winning; Assert.True(table.CheckLines(member));
        table.LuckyNumber = losing; Assert.False(table.CheckLines(member));
        table.LuckyNumber = 0; Assert.False(table.CheckLines(member));
        table.LuckyNumber = 37; Assert.False(table.CheckLines(member));
    }

    [Fact]
    public void MemberCostUsesBothLongSumsThenUncheckedUintAndPreservesNullElementFault()
    {
        var member = new Table.Member();
        member.MyLuckNumber[1] = new() { Number = 1, BetPrice = uint.MaxValue };
        member.MyLuckExtra[151] = new() { Number = 151, BetPrice = 2 };
        Assert.Equal(1u, member.MyCost);
        member.MyLuckExtra[152] = null!;
        Assert.Throws<NullReferenceException>(() => _ = member.MyCost);
    }

    [Fact]
    public void ContainNoChecksInputForStraightButCurrentLuckyNumberForExtrasWithoutEarlySuccessReturn()
    {
        var table = new Table { LuckyNumber = 1 };
        var member = new Table.Member();
        member.MyLuckNumber[37] = new() { Number = 37, BetPrice = 5 };
        table.RegistredPlayers[1] = member;
        Assert.True(table.CountainNo(37)); Assert.False(table.CountainNo(1));
        member.MyLuckExtra[151] = new() { Number = 151, BetPrice = 5 };
        Assert.True(table.CountainNo(255));
        member.MyLuckExtra[151] = null!;
        Assert.Throws<NullReferenceException>(() => table.CountainNo(37));
    }

    [Fact]
    public void ColorAllocationUsesFirstAvailableAndReturnsNoneWhenEverySourceColorIsOccupied()
    {
        var table = new Table();
        for (uint i = 0; i < 5; i++)
        {
            table.GeneratePlayerColor(out var color);
            Assert.Equal((Color)i, color);
            table.RegistredPlayers[i] = new() { Color = color };
            Assert.True(table.ContainColor(color));
        }
        table.GeneratePlayerColor(out var none); Assert.Equal(Color.None, none);
        Assert.False(table.ContainColor(Color.Watch));
        table.RegistredPlayers.TryRemove(2, out _);
        table.GeneratePlayerColor(out var gap); Assert.Equal(Color.Green, gap);
    }

    [Fact]
    public Task RateUsesIndependentSeededRandAndStrictGreaterComparison() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = new Table { Rand = new Random(1) };
        Assert.True(table.Rate(25)); // Native seed1 sequence24,11,46,77,65.
        Assert.False(table.Rate(11)); Assert.True(table.Rate(47));
        Assert.False(table.Rate(77)); Assert.True(table.Rate(66));
        table.Rand = new Random(1); Assert.False(table.Rate(24));
        Assert.False(table.Rate(-1)); Assert.True(table.Rate(100));
        table.Rand = null!; Assert.Throws<NullReferenceException>(() => table.Rate(1));
    });

    [Fact]
    public Task GenerateLuckyNumberUsesSeparatePoolAndResetsOnlySmallWin() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var table = new Table { Rand = null! };
        var member = new Table.Member { smalwin = 99, Noumber = 7, Winning = 4, Betting = 3 };
        member.MyLuckNumber[1] = new() { Number = 1, BetPrice = 2 };
        table.RegistredPlayers[1] = member;
        LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(1);
        table.GenerateLuckyNumber();
        Assert.Equal((byte)9, table.LuckyNumber); // Next(0,38) at seed1.
        Assert.Equal(0u, member.smalwin); Assert.Equal(7u, member.Noumber);
        Assert.Equal(4u, member.Winning); Assert.Equal(3u, member.Betting); Assert.Single(member.MyLuckNumber);
        Assert.Null(table.Rand);
    });

    [Fact]
    public void ResetClearsBetAliasesNotResultsAndResetsActualPrivateRoundFlag()
    {
        var table = new Table { StampRound = new DateTime(2020, 1, 1), LuckyNumber = 25 };
        var member = new Table.Member { Winning = 10, Betting = 5, smalwin = 4, Noumber = 3 };
        table.RegistredPlayers[1] = member;
        member.MyLuckNumber[1] = new() { Number = 1, BetPrice = 2 };
        member.MyLuckExtra[151] = new() { Number = 151, BetPrice = 3 };
        var numbers = member.MyLuckNumber; var extras = member.MyLuckExtra;
        var reset = typeof(Table).GetField("Reset", BindingFlags.NonPublic | BindingFlags.Instance)!;
        reset.SetValue(table, true);
        DateTime before = DateTime.Now.AddSeconds(30); table.ResetRoulette(); DateTime after = DateTime.Now.AddSeconds(30);
        Assert.InRange(table.TimerStamp, before, after); Assert.False((bool)reset.GetValue(table)!);
        Assert.Same(numbers, member.MyLuckNumber); Assert.Same(extras, member.MyLuckExtra);
        Assert.Empty(numbers); Assert.Empty(extras); Assert.Equal(10u, member.Winning); Assert.Equal(5u, member.Betting);
        Assert.Equal(4u, member.smalwin); Assert.Equal(3u, member.Noumber); Assert.Equal((byte)25, table.LuckyNumber);
        Assert.Equal(new DateTime(2020, 1, 1), table.StampRound);
        table.TimerStamp = DateTime.Now.AddSeconds(-10); Assert.Equal(0, table.GetTimerStamp);
        table.TimerStamp = DateTime.Now.AddSeconds(100); Assert.InRange(table.GetTimerStamp, 98, 101);
    }
}
