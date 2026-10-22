using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task PlayerReceiveTeamQualifierMagicSendsScoreAndThunderDamageOnce()
    {
        var result = await RunTeamArenaPlayerReceiveQualifierThunderAsync(targetMinHitPoints: 5_000);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        Assert.Single(
            ParseMsgUpdates(result.TargetSent),
            update =>
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000 - ExpectedPlayerThunderDamage));

        foreach (var spell in new[]
                 {
                     Assert.Single(ParseSpellAnimations(result.CasterSent)),
                     Assert.Single(ParseSpellAnimations(result.TargetSent)),
                 })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal((ushort)832, spell.X);
            Assert.Equal((ushort)584, spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        AssertTeamArenaQualifierScore(
            Assert.Single(ParseTeamArenaMatchScores(result.CasterSent)),
            expectedDamageOne: ExpectedPlayerThunderDamage,
            expectedDamageTwo: 0);
        AssertTeamArenaQualifierScore(
            Assert.Single(ParseTeamArenaMatchScores(result.TargetSent)),
            expectedDamageOne: ExpectedPlayerThunderDamage,
            expectedDamageTwo: 0);
    }

    [Fact]
    public async Task PlayerReceiveTeamQualifierLethalMagicDoesNotSendNonLethalScore()
    {
        var result = await RunTeamArenaPlayerReceiveQualifierThunderAsync(targetMinHitPoints: 100);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);

        Assert.Empty(ParseTeamArenaMatchScores(result.CasterSent));
        Assert.Empty(ParseTeamArenaMatchScores(result.TargetSent));
    }

    private static async Task<PlayerReceiveTeamQualifierThunderResult> RunTeamArenaPlayerReceiveQualifierThunderAsync(
        long targetMinHitPoints)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var teamArenaStates = new TeamArenaSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 700,
            attributes: CreateTeamArenaPlayerReceiveQualifierAttributes());
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetMinHitPoints,
            mapId: 700,
            attributes: CreateTeamArenaPlayerReceiveQualifierAttributes());
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        ActivateTeamArenaQualifierMatch(teamStates, teamArenaStates, casterConnection, targetConnection);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamArenaStates: teamArenaStates,
            TeamStates: teamStates);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        return new PlayerReceiveTeamQualifierThunderResult(
            sessions,
            casterConnection,
            targetConnection,
            casterSent,
            targetSent);
    }

    private static Dictionary<string, string> CreateTeamArenaPlayerReceiveQualifierAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "42",
        };
    }

    private static void ActivateTeamArenaQualifierMatch(
        TeamSessionStateStore teamStates,
        TeamArenaSessionStateStore teamArenaStates,
        ConquerTcpConnection casterConnection,
        ConquerTcpConnection targetConnection)
    {
        teamStates.CreateTeam(casterConnection, PlayerUid);
        teamStates.CreateTeam(targetConnection, TargetPlayerUid);

        Assert.False(teamArenaStates.RegisterWaiting(casterConnection, arenaPoints: 4_500, out _));
        Assert.True(teamArenaStates.RegisterWaiting(targetConnection, arenaPoints: 4_300, out var pair));
        Assert.Same(casterConnection, pair.First);
        Assert.Same(targetConnection, pair.Second);
        Assert.False(teamArenaStates.TryAcceptCountdown(casterConnection, out _));
        Assert.True(teamArenaStates.TryAcceptCountdown(targetConnection, out var activePair));
        Assert.Same(casterConnection, activePair.First);
        Assert.Same(targetConnection, activePair.Second);
    }

    private static List<LegacyTeamArenaMatchScorePacket> ParseTeamArenaMatchScores(IEnumerable<byte[]> frames)
    {
        var scores = new List<LegacyTeamArenaMatchScorePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyTeamArenaMatchScorePacket.TryParse(ReadFrame(bytes), out var score))
            {
                scores.Add(score);
            }
        }

        return scores;
    }

    private static void AssertTeamArenaQualifierScore(
        LegacyTeamArenaMatchScorePacket score,
        uint expectedDamageOne,
        uint expectedDamageTwo)
    {
        Assert.Equal(PlayerUid, score.UidOne);
        Assert.Equal(0U, score.RankOne);
        Assert.Equal("Caster", score.NameOne);
        Assert.Equal(expectedDamageOne, score.DamageOne);
        Assert.Equal(TargetPlayerUid, score.UidTwo);
        Assert.Equal(0U, score.RankTwo);
        Assert.Equal("Target", score.NameTwo);
        Assert.Equal(expectedDamageTwo, score.DamageTwo);
        Assert.Equal(LegacyTeamArenaMatchScorePacket.LegacyResult, score.Result);
    }

    private sealed record PlayerReceiveTeamQualifierThunderResult(
        WorldSessionStore Sessions,
        ConquerTcpConnection CasterConnection,
        ConquerTcpConnection TargetConnection,
        List<byte[]> CasterSent,
        List<byte[]> TargetSent);
}
