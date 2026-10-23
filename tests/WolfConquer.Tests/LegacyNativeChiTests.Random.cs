using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Locked = WolfConquer.Core.World.LegacyNativeMsgChiInfo.LockedFlags;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeChiTests
{
    [Theory]
    [InlineData(0,10,100,100)] [InlineData(1,10,185,200)] [InlineData(2,10,185,200)]
    [InlineData(3,10,185,200)] [InlineData(4,10,185,200)] [InlineData(5,10,185,200)]
    [InlineData(6,1000,3250,3500)] [InlineData(7,500,1900,2000)] [InlineData(8,500,2200,2500)]
    [InlineData(9,50,200,250)] [InlineData(10,50,450,500)] [InlineData(11,50,250,300)]
    [InlineData(12,50,450,500)] [InlineData(13,50,250,300)] [InlineData(14,10,100,100)]
    [InlineData(-1,10,100,100)] [InlineData(999,10,100,100)]
    public void ChiValueTablesKeepEveryNativeAttributeAndUnknownFallback(int type, int minimum, int medium, int maximum)
    {
        Assert.Equal(minimum, Chi.ChiMinValues((Stat)type));
        Assert.Equal(medium, Chi.ChiMedValues((Stat)type));
        Assert.Equal(maximum, Chi.ChiMaxValues((Stat)type));
    }

    [Fact]
    public Task EpicStatsReturnExactMaximumBeforeMetadataAndWithoutEitherRandomDraw() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var poolControl = new LegacyNativeSafeRandom(71); _ = poolControl.Next(); // Physical player birth.
        var power = MinimumPower(Kind.Dragon);
        LegacyNativeFateExpTable.Values = null!;
        Assert.Equal((ushort)200, power.StatsRand(new Attr(Stat.CriticalStrike, 0, false)));
        Assert.Equal((ushort)3500, power.StatsRand(new Attr(Stat.HPAdd, 0, false), epic: true));
        Assert.Equal((ushort)100, power.StatsRand(new Attr((Stat)999, 0, true)));
        Assert.Equal(273262797, Power.Rand.Next());
        Assert.Equal(poolControl.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task NonEpicBelowSeventyRollConsumesOneRealRandomLiteDrawAndNoPercentRollWithoutMetadata() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var poolControl = new LegacyNativeSafeRandom(71); _ = poolControl.Next();
        var power = MinimumPower(Kind.Dragon);
        Assert.Equal((ushort)131, power.StatsRand(new Attr(Stat.AddMagicDefense, 0, false), epic: false));
        Assert.Equal(512644276, Power.Rand.Next());
        Assert.Equal(poolControl.Next(), LegacyNativePoolRandom.GetRandom.Next());
    });

    [Fact]
    public Task NonEpicUsesCurrentFateMinimumRatherThanReturningAnUnderMinimumFirstDraw() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Dragon);
        var fate = new LegacyNativeFateExpTable.Value { ChiType = Kind.Dragon, MinScore = 60, StudyLuck = 0 };
        LegacyNativeFateExpTable.Values[1] = fate;
        ushort rolled = power.StatsRand(new Attr(Stat.AddMagicDefense, 0, false), epic: false);
        Assert.InRange(rolled, (ushort)150, (ushort)250);
        Assert.NotEqual((ushort)131, rolled);
        fate.MinScore = 0;
        Power.Rand.Reseed(97);
        Assert.Equal((ushort)131, power.StatsRand(new Attr(Stat.AddMagicDefense, 0, false), epic: false));
    });

    [Fact]
    public Task NonEpicNullMetadataFaultOccursAfterValueDrawUnlikeEpicEarlyReturn() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Dragon);
        LegacyNativeFateExpTable.Values = null!;
        Assert.Throws<NullReferenceException>(() => power.StatsRand(new Attr(Stat.AddMagicDefense, 0, false), epic: false));
        Assert.Equal(512644276, Power.Rand.Next());
    });

    [Fact]
    public Task AllLockedRerollOnlyMarksFlagsWithoutRandomMetadataPacketOrBroadcastAccess() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = MinimumPower(Kind.Dragon);
        LegacyNativeFateExpTable.Values = null!;
        LegacyNativePoolClients.GamePoll = null;
        scope.NullBin();
        power.Reroll(Locked.All);
        Assert.All(power.Attributes, attr => { Assert.True(attr.Locked); Assert.Equal(Stat.CriticalStrike, attr.Type); Assert.Equal((ushort)10, attr.Value); });
        Assert.Equal(273262797, Power.Rand.Next());
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RerollUsesNativeRedundantDrawThenDistinctTypeSelectionAndBroadcastsEachMaxRollInOrder() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Dragon) { Name = "ChiTester" };
        scope.Clients[17] = scope.Client;

        power.Reroll(Locked.None);

        Assert.Equal(new[] { Stat.SkillCriticalStrike, Stat.AddMagicDefense, Stat.Breakthrough, Stat.HPAdd }, power.Attributes.Select(attr => attr.Type));
        Assert.Equal(new ushort[] {200,250,200,3500}, power.Attributes.Select(attr => attr.Value));
        Assert.All(power.Attributes, attr => Assert.False(attr.Locked));
        Assert.Equal(600505098, Power.Rand.Next()); // Exactly eight genuine type draws consumed.
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(4, frames.Length);
        for (int index = 0; index < 4; index++) Assert.Equal(ChiMessage(Kind.Dragon, power.Attributes[index]), frames[index]);
        Assert.False(power.UnLocked); // Reroll itself never sets the power unlock bit.
    });

    [Fact]
    public Task RerollRejectsTypeCollisionAgainstStillUnprocessedSlotsAndRetainsAllSuccessfulMutations() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Tiger) { Name = "ChiTester", Attributes = Enumerable.Repeat(new Attr(Stat.SkillCriticalStrike, 1, true), 4).ToArray() };
        scope.Clients[17] = scope.Client;
        power.Reroll(Locked.None);
        Assert.Equal(new[] {Stat.MagicDamageIncrease,Stat.MagicDamageDecrease,Stat.AddMagicDefense,Stat.PhysicalDamageDecrease}, power.Attributes.Select(attr => attr.Type));
        Assert.Equal(1082358426, Power.Rand.Next()); // Nine draws: first slot rejected duplicate type2.
        Assert.Equal(4, scope.Queue.Count);
        Assert.All(power.Attributes, attr => Assert.False(attr.Locked));
    });

    [Fact]
    public Task PartialLockedFlagsKeepExactAttributesAndOnlyUnlockedSlotsConsumeDrawsOrBroadcast() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Phoenix) { Name = "ChiTester" };
        scope.Clients[17] = scope.Client;
        power.Reroll(Locked.First | Locked.Third);
        Assert.Equal(new[] {Stat.None,Stat.SkillCriticalStrike,Stat.None,Stat.AddMagicDefense}, power.Attributes.Select(attr => attr.Type));
        Assert.Equal(new[] {true,false,true,false}, power.Attributes.Select(attr => attr.Locked));
        Assert.Equal(new ushort[] {0,200,0,250}, power.Attributes.Select(attr => attr.Value));
        Assert.Equal(798217588, Power.Rand.Next());
        byte[][] frames = scope.Queue.ToArray(); Assert.Equal(2, frames.Length);
        Assert.Equal(ChiMessage(Kind.Phoenix, power.Attributes[1]), frames[0]);
        Assert.Equal(ChiMessage(Kind.Phoenix, power.Attributes[3]), frames[1]);
    });

    [Fact]
    public Task RerollPacketOwnerFaultLeavesFirstTypeValueAndPriorLockedFlagBeforeLaterSlots() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Dragon) { Attributes = Enumerable.Repeat(new Attr(Stat.None, 0, true), 4).ToArray() };
        scope.NullBin();
        Assert.Throws<NullReferenceException>(() => power.Reroll(Locked.None));
        Assert.Equal(Stat.SkillCriticalStrike, power.Attributes[0].Type);
        Assert.Equal((ushort)200, power.Attributes[0].Value);
        Assert.True(power.Attributes[0].Locked); // Native unlock write is after broadcast.
        Assert.Equal(default(Stat), power.Attributes[1].Type); Assert.Equal((ushort)0, power.Attributes[1].Value); Assert.True(power.Attributes[1].Locked);
        Assert.Equal(935566525, Power.Rand.Next());
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RerollBroadcastNullPoolFaultOccursAfterRealMessageWriteAndBeforeUnlock() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var power = new Power(Kind.Turtle) { Name = "ChiTester", Attributes = Enumerable.Repeat(new Attr(Stat.None, 0, true), 4).ToArray() };
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => power.Reroll(Locked.None));
        Assert.Equal(Stat.SkillCriticalStrike, power.Attributes[0].Type); Assert.True(power.Attributes[0].Locked);
        Assert.Equal(ChiMessage(Kind.Turtle, power.Attributes[0]), NativeSpellTestFixture.Bytes(scope.Recycled(0)));
        Assert.Equal(0, scope.Recycled(0).Position); // Real using/Dispose seeks the retained packet to0.
        Assert.Equal(935566525, Power.Rand.Next());
        Assert.Empty(scope.Queue);
    });
}
