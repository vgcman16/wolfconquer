using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task SavedStateBirthRetainsNativeDefaultsNullAndDistinctClientPlayerBanWidths() => NativePacketTestThread.Run(() =>
    {
        DateTime before = DateTime.Now;
        using var s = new Scope();
        var p = s.Client.Player;
        DateTime after = DateTime.Now;
        Assert.InRange(p.CanChangeWindWalkerFree, before, after);
        Assert.Equal("", p.PinCodeAnima);
        Assert.Null(p.MyKillerName);
        Assert.Equal((byte)3, p.TowerOfMysterychallenge);
        Assert.Equal(LegacyNativeTowerRewardType.Normal, p.TOM_Reward);
        Assert.Equal(LegacyNativePlayerRuntime.RechargeType.None, p.RechargeProgress);
        Assert.Equal((byte)0, p.AnimaSkin);
        Assert.Equal((ushort)0, p.Avatar);
        Assert.Equal(0u, p.BanCount);
        Assert.Equal((byte)0, s.Client.BanCount);
        Assert.Equal(typeof(uint), typeof(LegacyNativePlayerRuntime).GetField("BanCount")!.FieldType);
        Assert.Equal(typeof(byte), typeof(LegacyNativeGameClientRuntime).GetField("BanCount")!.FieldType);
        Assert.Equal(0u, p.PMap); Assert.Equal((ushort)0, p.PMapX); Assert.Equal((ushort)0, p.PMapY);
        Assert.Equal(0u, p.PDinamycID); Assert.Equal(default, p.JoinOnflineTG);
        Assert.Equal((byte)0, p.ExpBallUsed); Assert.Equal((byte)0, p.BDExp);
        Assert.Equal(0, p.Day); Assert.Equal(0u, p.HuntingBlessing); Assert.Equal(0u, p.BlessTime);
        Assert.Equal(0u, p.TOMRefreshReward); Assert.Equal(0u, p.TowerOfMysteryChallengeFlag);
        Assert.Equal(0u, p.TournamentKills); Assert.Equal(0u, p.ArenaKills); Assert.Equal(0u, p.ArenaDeads);
        Assert.Equal(0u, p.OnlineMinutes); Assert.Equal(0u, p.OnlinePointsPK);
        Assert.Null(p.RacePotions); Assert.Equal(0, p.FootballTeamID);
        Assert.Equal(0u, p.FootBallMatchPoints); Assert.Equal(0u, p.MyFootBallPoints); Assert.Equal(0u, p.SpecialGarment);
        Assert.Equal(0u, s.Client.Vigor); Assert.False(s.Client.RaceDizzy); Assert.False(s.Client.RaceFrightened);
        Assert.False(s.Client.RaceGuard); Assert.False(s.Client.RaceDecelerated); Assert.False(s.Client.RaceExcitement);
    });

    [Fact]
    public Task ChampionAssignmentRaisesHistoryButNeverLowersItOrMutatesToday() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var p = s.Client.Player; p.TodayChampionPoints = 77;
        p.ChampionPoints = 100; Assert.Equal(100u, p.HistoryChampionPoints);
        p.ChampionPoints = 20; Assert.Equal(20u, p.ChampionPoints); Assert.Equal(100u, p.HistoryChampionPoints);
        p.ChampionPoints = uint.MaxValue; Assert.Equal(uint.MaxValue, p.HistoryChampionPoints);
        p.ChampionPoints = 0; Assert.Equal(0u, p.ChampionPoints); Assert.Equal(uint.MaxValue, p.HistoryChampionPoints);
        Assert.Equal(77u, p.TodayChampionPoints);
    });

    [Theory]
    [InlineData(0ul, 0u)]
    [InlineData(1ul, 1u)]
    [InlineData(2147483647ul, 31u)]
    [InlineData(2147483648ul, 0u)]
    [InlineData(9223372036854775808ul, 0u)]
    [InlineData(18446744073709551615ul, 31u)]
    public Task DailyDayCountOnlyCountsTheNativeFirst31Bits(ulong flags, uint expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Player.DailySignUpDays = flags;
        Assert.Equal(expected, s.Client.Player.DailyDays);
    });

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, false, true)]
    public Task ChatBanQueryPreservesTimestampAndPermanentBranches(bool permanent, bool active, bool future, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var p = s.Client.Player;
        p.PermenantBannedChat = permanent; p.IsBannedChat = active;
        p.BannedChatStamp = future ? DateTime.MaxValue : DateTime.MinValue;
        Assert.Equal(expected, p.IsStillBanned);
    });

    [Fact]
    public Task CompleteRewardEnumsRetainNativeGapAnd64BitRechargeValues() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.Equal(new byte[] { 0, 1, 2, 3, 5, 6 }, Enum.GetValues<LegacyNativeTowerRewardType>().Select(x => (byte)x));
        Assert.Equal(22, Enum.GetValues<LegacyNativePlayerRuntime.RechargeType>().Length);
        Assert.Equal(0x100000ul, (ulong)LegacyNativePlayerRuntime.RechargeType.InnerPower10000);
        Assert.Equal(0x80000ul, (ulong)LegacyNativePlayerRuntime.RechargeType.GoldPrize);
        Assert.Equal(0x20ul, (ulong)LegacyNativePlayerRuntime.RechargeType.ConquerPoints225);
    });
}
