using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearReceiverTests
{
    [Fact]
    public Task ZeroCountPracticeUsesActualRecipientUidKeepsPhysicalPowersAndEmitsBeforeRank() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseAipPower = 5; powers.ArmedAipPower = 7; powers.OverLoadAipPower = 11; powers.Points = 55;
        s.Input(AirRequest(4, requestUid: 0, power: 701), opcode: 123);

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        AirResponse(Assert.Single(s.Queue), 1, sensePower: 5, armedPower: 7, overloadPower: 11, points: 55);
        Assert.Same(s.Gear, s.User.CombatGear); Assert.Same(powers, s.Gear.AirPowers);
        Assert.Equal(1u, powers.SenseLevel); Assert.Equal(55u, powers.Points);
        Assert.Empty(s.Native.Storage.ClientSpells);
        PirateRanks(23);
        s.UnchangedInput();
    });

    [Fact]
    public Task InsufficientPracticeStillSendsPhysicalStateToActualUserRatherThanRequestUid() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseAipPower = 5; powers.ArmedAipPower = 7; powers.OverLoadAipPower = 11; powers.Points = 1999;
        s.Input(AirRequest(4, requestUid: 2, power: 701));

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        AirResponse(Assert.Single(s.Queue), 1, sensePower: 5, armedPower: 7, overloadPower: 11, points: 1999);
        Assert.Equal(1999u, powers.Points); Assert.Equal(5u, powers.SenseAipPower);
        Assert.Empty(s.Native.Storage.ClientSpells);
        PirateRanks(23);
        s.UnchangedInput();
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(-1)]
    public Task NonPracticeStillTeachesSenseArmedOverloadInExactOrderAndUpdatesRealPirateRanks(int type) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseLevel = 2; powers.ArmedLevel = 3; powers.OverLoadLevel = 4;
        powers.SenseAipPower = 5; powers.ArmedAipPower = 7; powers.OverLoadAipPower = 11;
        s.Input(AirRequest(type, requestUid: uint.MaxValue, power: uint.MaxValue));

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        Assert.Equal(new[] { ((ushort)21720, (ushort)2), ((ushort)21710, (ushort)3), ((ushort)21490, (ushort)4) }, s.Queue.Select(Spell));
        Assert.Equal(3, s.Native.Storage.ClientSpells.Count);
        Assert.Equal((ushort)2, s.Native.Storage.ClientSpells[21720].Level);
        Assert.Equal((ushort)3, s.Native.Storage.ClientSpells[21710].Level);
        Assert.Equal((ushort)4, s.Native.Storage.ClientSpells[21490].Level);
        Assert.Same(powers, s.Gear.AirPowers);
        Assert.Equal((2u, 3u, 4u), (powers.SenseLevel, powers.ArmedLevel, powers.OverLoadLevel));
        Assert.Equal(25, s.Native.Packet.Size); Assert.Equal(4, s.Native.Packet.Position);
        Assert.Equal(s.Queue.Last(), NativeSpellTestFixture.Bytes(s.Native.Packet));
        PirateRanks(23);
    });

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public Task PostProcessLearningUsesStrictGreaterThanOneWithoutSendNormalizationOnNonPractice(uint level) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseLevel = powers.ArmedLevel = powers.OverLoadLevel = level;
        s.Input(AirRequest());

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        Assert.Empty(s.Queue); Assert.Empty(s.Native.Storage.ClientSpells);
        Assert.Equal((level, level, level), (powers.SenseLevel, powers.ArmedLevel, powers.OverLoadLevel));
        PirateRanks(0);
        s.UnchangedInput();
    });

    [Fact]
    public Task PostProcessTestsUintThresholdBeforeUncappedUshortCastAndReplacesActualSpellOwner() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseLevel = 65536; powers.ArmedLevel = 65537; powers.OverLoadLevel = uint.MaxValue;
        var retained = new LegacyNativeMsgSpell { ID = 21720, Level = 5, Experience = 88, SoulLevel = 2, PreviousLevel = 9 };
        s.Native.Storage.ClientSpells[21720] = retained;
        s.Input(AirRequest());

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        Assert.Equal(new[] { ((ushort)21720, (ushort)0), ((ushort)21710, (ushort)1), ((ushort)21490, ushort.MaxValue) }, s.Queue.Select(Spell));
        Assert.NotSame(retained, s.Native.Storage.ClientSpells[21720]);
        Assert.Equal((ushort)5, retained.Level); Assert.Equal(88, retained.Experience);
        Assert.Equal((byte)2, retained.SoulLevel); Assert.Equal((byte)9, retained.PreviousLevel);
        Assert.Equal((65536u, 65537u, uint.MaxValue), (powers.SenseLevel, powers.ArmedLevel, powers.OverLoadLevel));
        PirateRanks(0);
    });

    [Fact]
    public Task ReceiverReadsCurrentRetainedClientGearAndPowersNotAnOlderDetachedOwner() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Gear.AirPowers.SenseLevel = 9; s.Gear.AirPowers.SenseAipPower = 999;
        var powers = new Gear.Powers { Id = 41, SenseLevel = 2, SenseAipPower = 11 };
        var current = new LegacyNativeMyCombatGear(s.User) { AirPowers = powers };
        s.User.CombatGear = current;
        s.Input(AirRequest());

        Receive("ProcessAirPowerUp", s.User, s.Native.Packet);

        Assert.Equal(((ushort)21720, (ushort)2), Spell(Assert.Single(s.Queue)));
        Assert.Same(current, s.User.CombatGear); Assert.Same(powers, current.AirPowers);
        Assert.Equal(9u, s.Gear.AirPowers.SenseLevel); Assert.Equal(999u, s.Gear.AirPowers.SenseAipPower);
        PirateRanks(11);
    });

    [Fact]
    public Task MissingSpellStorageFaultIsAfterPracticeResponseAndMutationsButBeforeRankPublication() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseLevel = 2; powers.SenseAipPower = 120000;
        s.User.MySpells = null!;
        s.Input(AirRequest(4, requestUid: 0, power: 701));

        Assert.Throws<NullReferenceException>(() => Receive("ProcessAirPowerUp", s.User, s.Native.Packet));

        AirResponse(Assert.Single(s.Queue), 3, armed: 1, overload: 1, sensePower: 120000);
        Assert.Equal((3u, 1u, 1u), (powers.SenseLevel, powers.ArmedLevel, powers.OverLoadLevel));
        Assert.Null(s.User.MySpells); Assert.Empty(s.Native.Storage.ClientSpells);
        Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        s.UnchangedInput();
    });

    [Fact]
    public Task RealLateRankFaultKeepsAllThreeLearnTellsAndTheFirstWeeklyRankPublication() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var powers = s.Gear.AirPowers;
        powers.SenseLevel = 2; powers.ArmedLevel = 3; powers.OverLoadLevel = 4;
        powers.SenseAipPower = 5; powers.ArmedAipPower = 7; powers.OverLoadAipPower = 11;
        Rank.Ranks.Remove(Rank.Type.Overall_Pirate);
        s.Input(AirRequest());

        Assert.Throws<KeyNotFoundException>(() => Receive("ProcessAirPowerUp", s.User, s.Native.Packet));

        Assert.Equal(new[] { ((ushort)21720, (ushort)2), ((ushort)21710, (ushort)3), ((ushort)21490, (ushort)4) }, s.Queue.Select(Spell));
        Assert.Equal(3, s.Native.Storage.ClientSpells.Count);
        var entry = Assert.Single(Rank.Ranks[Rank.Type.WEEK_Pirate].RankingList).Value;
        Assert.Equal(17u, entry.UID); Assert.Equal(23u, entry.TotalPoints); Assert.Equal(Rank.Type.WEEK_Pirate, entry.Type);
        Assert.False(Rank.Ranks.ContainsKey(Rank.Type.Overall_Pirate));
        Assert.Equal(25, s.Native.Packet.Size); Assert.Equal(4, s.Native.Packet.Position);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullPhysicalAirOwnerFaultsWithoutAllocationWhetherInsideProcessOrPostProcess(bool practice) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        s.Gear.AirPowers = null!;
        s.Input(AirRequest(practice ? 4 : 0, power: 999));

        Assert.Throws<NullReferenceException>(() => Receive("ProcessAirPowerUp", s.User, s.Native.Packet));

        Assert.Null(s.Gear.AirPowers); Assert.Empty(s.Queue); Assert.Empty(s.Native.Storage.ClientSpells);
        Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        s.UnchangedInput();
    });

    [Fact]
    public Task FalseAirGateStillDeserializesThenNullForeachFaultsBeforeNullPowersSpellsOrRank() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Table.m_Load = false;
        s.Gear.AirPowers = null!; s.User.MySpells = null!;
        s.Input(AirRequest(4, requestUid: uint.MaxValue, power: 701));

        Assert.Throws<NullReferenceException>(() => Receive("ProcessAirPowerUp", s.User, s.Native.Packet));

        Assert.False(Table.m_Load); Assert.Null(s.Gear.AirPowers); Assert.Null(s.User.MySpells);
        Assert.Empty(s.Queue); Assert.All(Rank.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        s.UnchangedInput();
    });
}
