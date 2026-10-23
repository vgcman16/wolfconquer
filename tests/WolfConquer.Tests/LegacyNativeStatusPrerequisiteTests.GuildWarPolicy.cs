using WolfConquer.Core.World;
using N = WolfConquer.Core.World.LegacyNativeGuildWar.NPCID;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeStatusPrerequisiteTests
{
    [Theory]
    [InlineData(false, 1, 0u, false)] [InlineData(true, 0, 0u, false)]
    [InlineData(true, 1, 7u, false)] [InlineData(true, 1, 0u, true)]
    public Task CompleteGuildPillarPolicyRejectsMissingGuildDeadPillarAndSameWinner(bool guild, int hp, uint winner, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar { Proces = LegacyNativeProcesType.Alive };
        if (guild) { s.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Wolf"); s.Player.MyGuild.Info.GuildID = 7; s.Player.GuildID = 7; }
        war.Winner.GuildID = winner; var npc = new LegacyNativeSobNpcRuntime { UID = 55, HitPoints = hp };
        war.NPC_INFO[N.GuildPillar] = new() { Npc = npc };
        Assert.Equal(expected, war.Verified(s.Player, npc)); war.Proces = LegacyNativeProcesType.Dead;
        Assert.False(war.Verified(s.Player, npc));
    });

    [Theory]
    [InlineData((uint)N.GuildWithHonorPillar)] [InlineData((uint)N.ExorcimPillar)] [InlineData((uint)N.TenacityPillar)]
    public Task OptionalPillarsUseTheirOwnGenuineOwnerGuildAndActiveProcess(uint kind) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar { Proces = LegacyNativeProcesType.Alive };
        s.Player.MyGuild = LegacyNativeGuildRuntime.CreateLoadedGuild("Wolf"); s.Player.MyGuild.Info.GuildID = 7; s.Player.GuildID = 7;
        war.NPC_INFO[N.GuildPillar] = new() { Npc = new() { UID = 55 } };
        var target = new LegacyNativeSobNpcRuntime { UID = 66, GuildID = 8 }; war.NPC_INFO[(N)kind] = new() { Npc = target };
        Assert.True(war.Verified(s.Player, target)); target.GuildID = 7; Assert.False(war.Verified(s.Player, target));
        target.GuildID = 8; war.Proces = LegacyNativeProcesType.Idle; Assert.False(war.Verified(s.Player, target));
    });

    [Fact]
    public Task FullPolicyPreservesUnconditionalGuildPillarLookupFaultPrefix() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar(); var target = new LegacyNativeSobNpcRuntime { UID = 99 };
        Assert.Throws<KeyNotFoundException>(() => war.Verified(s.Player, target));
        war.NPC_INFO[N.GuildPillar] = new(); Assert.Throws<NullReferenceException>(() => war.Verified(s.Player, target));
        war.NPC_INFO[N.GuildPillar].Npc = new() { UID = 55 }; Assert.True(war.Verified(s.Player, target));
    });

    [Fact]
    public Task AvailablePlayerPointsUsesRealPoolSocketMapAndMemberFieldsWithUnsignedWrap() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var war = new LegacyNativeGuildWar(); s.Player.Map = 1038;
        s.Player.MyGuildMember = new() { GuildWarPoints = uint.MaxValue }; LegacyNativePoolClients.GamePoll![17] = s.Client;
        var second = NativeSpellTestFixture.CreateOwner(new()); second.Player.Map = 1038; second.Player.MyGuildMember = new() { GuildWarPoints = 2 };
        LegacyNativePoolClients.GamePoll[18] = second; Assert.Equal(1u, war.TotalAvailablePoints);
        second.Socket!.Alive = false; Assert.Equal(uint.MaxValue, war.TotalAvailablePoints);
        second.Socket.Alive = true; second.Player.Map = 1002; Assert.Equal(uint.MaxValue, war.TotalAvailablePoints);
        second.Player.Map = 1038; second.Player.MyGuildMember = null; Assert.Equal(uint.MaxValue, war.TotalAvailablePoints);
        s.Client.Socket!.Alive = false; Assert.Equal(0u, war.TotalAvailablePoints);
    });

    [Fact]
    public void CountDownKeepsSignedTime32DifferenceAndNoMapPrecondition()
    {
        var war = new LegacyNativeGuildWar { TimeLeft = LegacyNativeTime32.Now.AddSeconds(-5) }; Assert.Equal((ushort)0, war.CountDown);
        war.TimeLeft = LegacyNativeTime32.Now.AddSeconds(30); Assert.InRange(war.CountDown, (ushort)28, (ushort)30);
        Assert.Throws<NullReferenceException>(() => war.AvailablePoints);
    }
}
