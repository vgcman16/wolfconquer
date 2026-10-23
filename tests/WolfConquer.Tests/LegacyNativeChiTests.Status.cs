using WolfConquer.Core.Data;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    public static IEnumerable<object[]> RankBonusCases()
    {
        foreach (int rank in new[] { -1, 0, 1, 3, 4, 5, 20, 21, 50, 51 })
        {
            // Literal expected bonuses from each native boundary; not production calculations.
            int tier = rank is 1 or 3 ? 1 : rank == 4 ? 2 : rank == 5 ? 3 : rank == 20 ? 4 : rank is 21 or 50 ? 5 : 0;
            uint[][] dragon = [ [0,0,0,0], [5000,1000,300,300], [3000,600,200,200], [2938,588,197,197], [2000,400,152,152], [1500,300,100,100] ];
            uint[][] phoenix = [ [0,0,0,0], [3000,3000,1000,300], [2000,2000,600,200], [1969,1969,588,197], [1496,1496,400,152], [1000,2000,1000,300] ];
            uint[][] tiger = [ [0,0,0], [1500,1500,800], [1100,1100,500], [1083,1083,487], [828,828,292], [500,500,200] ];
            uint[][] turtle = [ [0,0,0], [150,150,800], [110,110,500], [108,108,487], [82,82,292], [50,50,200] ];
            var d = new uint[13]; d[0] = 400; d[5] = dragon[tier][0]; d[9] = dragon[tier][1]; d[10] = dragon[tier][2]; d[12] = dragon[tier][3];
            var p = new uint[13]; p[0] = 400; p[6] = phoenix[tier][0]; p[7] = phoenix[tier][1]; p[9] = phoenix[tier][2]; p[10] = phoenix[tier][3];
            var t = new uint[13]; t[0] = 400 + tiger[tier][0]; t[1] = tiger[tier][1]; t[2] = tiger[tier][2];
            var u = new uint[13]; u[0] = 400; u[3] = turtle[tier][0]; u[4] = turtle[tier][1]; u[2] = turtle[tier][2];
            yield return new object[] { (byte)Kind.Dragon, rank, d };
            yield return new object[] { (byte)Kind.Phoenix, rank, p };
            yield return new object[] { (byte)Kind.Tiger, rank, t };
            yield return new object[] { (byte)Kind.Turtle, rank, u };
        }
    }

    [Theory]
    [MemberData(nameof(RankBonusCases))]
    public Task ComputeStatusPreservesEveryNativeRankingBandAndTruncatedFraction(byte type, int rank, uint[] expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7); SeedTotals(chi);
        SetPower(chi, MinimumPower((Kind)type, rank: rank));
        Chi.ComputeStatus(chi);
        Assert.Equal(expected, Totals(chi));
        Assert.Empty(Chi.ChiPool);
        Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData((byte)1, 0)] [InlineData((byte)1, 51)]
    [InlineData((byte)2, 0)] [InlineData((byte)2, 51)]
    [InlineData((byte)3, 0)] [InlineData((byte)3, 51)]
    [InlineData((byte)4, 0)] [InlineData((byte)4, 51)]
    public Task PerfectScoreUsesTopBonusEvenWithUnrankedOrExcludedPosition(byte type, int rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7); var power = PerfectPower((Kind)type, rank: rank); SetPower(chi, power);
        Assert.Equal(400, power.Score);
        Chi.ComputeStatus(chi);
        var expected = new uint[13]; expected[0] = 8000;
        if ((Kind)type == Kind.Dragon) { expected[5] = 5000; expected[9] = 1000; expected[10] = 300; expected[12] = 300; }
        else if ((Kind)type == Kind.Phoenix) { expected[6] = 3000; expected[7] = 3000; expected[9] = 1000; expected[10] = 300; }
        else if ((Kind)type == Kind.Tiger) { expected[0] += 1500; expected[1] = 1500; expected[2] = 800; }
        else { expected[3] = 150; expected[4] = 150; expected[2] = 800; }
        Assert.Equal(expected, Totals(chi));
    });

    [Fact]
    public Task ComputeStatusAccumulatesAllThirteenOwnAndFateStatsWithNativeTenfoldCriticals() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        Attr[] all = [new(Stat.CriticalStrike,11,true), new(Stat.SkillCriticalStrike,12,false), new(Stat.Immunity,13,true), new(Stat.Breakthrough,14,false), new(Stat.Counteraction,15,true), new(Stat.HPAdd,1001,false), new(Stat.AddAttack,501,true), new(Stat.AddMagicAttack,501,false), new(Stat.AddMagicDefense,51,true), new(Stat.PhysicalDamageIncrease,51,false), new(Stat.MagicDamageIncrease,51,true), new(Stat.PhysicalDamageDecrease,51,false), new(Stat.MagicDamageDecrease,51,true)];
        var chi = new Chi(7) { Dragon = new Power(Kind.Dragon) { UnLocked = true, Attributes = all } };
        var fate = new LegacyNativeFateExpTable.Value { ChiType = Kind.Dragon, Attributes = Attr.ShallowCopy(all) };
        LegacyNativeFateExpTable.Values[1] = fate;
        SeedTotals(chi);

        Chi.ComputeStatus(chi);

        Assert.Equal(new uint[] { 220,240,260,28,30,2002,1002,1002,102,102,102,102,102 }, Totals(chi));
        Assert.Same(all, chi.Dragon.Attributes);
        Assert.True(all[0].Locked); // Locked is not a status-filter condition.
        Assert.Equal(6, chi.Dragon.Score); // Fate stats do not participate in Score.
        fate.Attributes[0].Value = 100;
        Chi.ComputeStatus(chi);
        Assert.Equal(1110u, chi.CriticalStrike);
        Assert.Equal(6, chi.Dragon.Score);
    });

    [Fact]
    public Task LockedPowersResetEveryTotalWithoutReadingNullArraysOrNullMetadataTable() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7); SeedTotals(chi);
        foreach (var power in chi) power.Attributes = null!;
        LegacyNativeFateExpTable.Values = null!;
        Chi.ComputeStatus(chi);
        Assert.All(Totals(chi), value => Assert.Equal(0u, value));
    });

    [Fact]
    public Task UnknownPowerAndStatKindsDoNotInventBonusesDespitePerfectScore() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7) { Dragon = PerfectPower((Kind)255, rank: 1) };
        Chi.ComputeStatus(chi);
        Assert.Equal(new uint[] {8000,0,0,0,0,0,0,0,0,0,0,0,0}, Totals(chi));
        chi.Dragon.Attributes = new[] { new Attr(Stat.LukyStrike, 100, true), new Attr((Stat)255, 100, false) };
        Chi.ComputeStatus(chi);
        Assert.All(Totals(chi), value => Assert.Equal(0u, value));
    });

    [Fact]
    public Task NullLaterPowerKeepsEarlierDragonTotalsAndNativeIterationFaultAfterReset() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7) { Dragon = MinimumPower(Kind.Dragon), Tiger = null! };
        SeedTotals(chi);
        Assert.Throws<NullReferenceException>(() => Chi.ComputeStatus(chi));
        Assert.Equal(new uint[] {400,0,0,0,0,0,0,0,0,0,0,0,0}, Totals(chi));
    });

    [Fact]
    public Task NullSynchronizationOwnerFaultsBeforeResetWhileNullMetadataFaultsAfterReset() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7) { Dragon = MinimumPower(Kind.Dragon) }; SeedTotals(chi);
        Chi.SynRoot = null!;
        Assert.Throws<ArgumentNullException>(() => Chi.ComputeStatus(chi));
        Assert.All(Totals(chi), value => Assert.Equal(uint.MaxValue, value));
        Chi.SynRoot = new object(); LegacyNativeFateExpTable.Values = null!;
        Assert.Throws<NullReferenceException>(() => Chi.ComputeStatus(chi));
        Assert.All(Totals(chi), value => Assert.Equal(0u, value));
    });

    [Fact]
    public Task NullFateAttributeArrayFaultsBeforeOwnAttributeApplicationAndLeavesResetState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var chi = new Chi(7) { Dragon = MinimumPower(Kind.Dragon) }; SeedTotals(chi);
        LegacyNativeFateExpTable.Values[1] = new() { ChiType = Kind.Dragon, Attributes = null! };
        Assert.Throws<ArgumentNullException>(() => Chi.ComputeStatus(chi));
        Assert.All(Totals(chi), value => Assert.Equal(0u, value));
    });
}
