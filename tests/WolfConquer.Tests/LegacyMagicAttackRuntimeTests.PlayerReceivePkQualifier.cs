using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task PlayerReceiveElitePkQualifierMagicSendsSourceMatchStatsAndThunderDamageOnce()
    {
        var result = await RunElitePkPlayerReceiveQualifierThunderAsync(targetMinHitPoints: 5_000);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        AssertElitePkQualifierScore(
            Assert.Single(ParseElitePkMatchStats(result.CasterSent)),
            expectedCasterPoints: ExpectedPlayerThunderDamage,
            expectedTargetPoints: 0);
        AssertElitePkQualifierScore(
            Assert.Single(ParseElitePkMatchStats(result.TargetSent)),
            expectedCasterPoints: ExpectedPlayerThunderDamage,
            expectedTargetPoints: 0);
        Assert.Contains("source=GameClient.UpdateQualifier ElitePKStats/ElitePkMatch", result.Log);
    }

    [Fact]
    public async Task PlayerReceiveTeamPkQualifierMagicSendsSourceMatchStatsAndThunderDamageOnce()
    {
        var result = await RunTeamPkPlayerReceiveQualifierThunderAsync(targetMinHitPoints: 5_000);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        AssertTeamPkQualifierScore(
            Assert.Single(ParseTeamPkMatchStats(result.CasterSent)),
            expectedCasterPoints: ExpectedPlayerThunderDamage,
            expectedTargetPoints: 0);
        AssertTeamPkQualifierScore(
            Assert.Single(ParseTeamPkMatchStats(result.TargetSent)),
            expectedCasterPoints: ExpectedPlayerThunderDamage,
            expectedTargetPoints: 0);
        Assert.Contains("source=GameClient.UpdateQualifier Team.PkMatch", result.Log);
    }

    private static async Task<PlayerReceivePkQualifierThunderResult> RunElitePkPlayerReceiveQualifierThunderAsync(
        long targetMinHitPoints)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var elitePkStates = new LegacyElitePkSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 700,
            attributes: CreatePkQualifierAttributes(dynamicId: 42, serverId: 1001));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetMinHitPoints,
            mapId: 700,
            attributes: CreatePkQualifierAttributes(dynamicId: 42, serverId: 1002));
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        elitePkStates.ActivateMatch(casterConnection, targetConnection, dynamicId: 42);

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            ElitePkStates: elitePkStates);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await SendThunderAtPlayerAsync(context, targetSession);

        return new PlayerReceivePkQualifierThunderResult(
            sessions,
            casterConnection,
            targetConnection,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static async Task<PlayerReceivePkQualifierThunderResult> RunTeamPkPlayerReceiveQualifierThunderAsync(
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
        var teamPkStates = new LegacyTeamPkSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 700,
            attributes: CreatePkQualifierAttributes(dynamicId: 42, serverId: 1001));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetMinHitPoints,
            mapId: 700,
            attributes: CreatePkQualifierAttributes(dynamicId: 42, serverId: 1002));
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        teamStates.CreateTeam(casterConnection, PlayerUid);
        teamStates.CreateTeam(targetConnection, TargetPlayerUid);
        teamStates.SetTeamName(casterConnection, "CasterTeam");
        teamStates.SetTeamName(targetConnection, "TargetTeam");
        teamPkStates.ActivateMatch(casterConnection, targetConnection, dynamicId: 42);

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates,
            TeamPkStates: teamPkStates);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await SendThunderAtPlayerAsync(context, targetSession);

        return new PlayerReceivePkQualifierThunderResult(
            sessions,
            casterConnection,
            targetConnection,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static async Task SendThunderAtPlayerAsync(
        WorldPacketHandlerContext context,
        WorldSession targetSession)
    {
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
    }

    private static Dictionary<string, string> CreatePkQualifierAttributes(uint dynamicId, uint serverId)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = dynamicId.ToString(),
            ["ServerID"] = serverId.ToString(),
        };
    }

    private static List<LegacyElitePkMatchStatsPacket> ParseElitePkMatchStats(IEnumerable<byte[]> frames)
    {
        var stats = new List<LegacyElitePkMatchStatsPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyElitePkMatchStatsPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                stats.Add(packet);
            }
        }

        return stats;
    }

    private static List<LegacyTeamPkMatchStatsPacket> ParseTeamPkMatchStats(IEnumerable<byte[]> frames)
    {
        var stats = new List<LegacyTeamPkMatchStatsPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyTeamPkMatchStatsPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                stats.Add(packet);
            }
        }

        return stats;
    }

    private static void AssertElitePkQualifierScore(
        LegacyElitePkMatchStatsPacket packet,
        uint expectedCasterPoints,
        uint expectedTargetPoints)
    {
        Assert.Equal(PlayerUid, packet.UidOne);
        Assert.Equal("Caster", packet.NameOne);
        Assert.Equal(1001U, packet.ServerIdOne);
        Assert.Equal(expectedCasterPoints, packet.PointsOne);
        Assert.Equal(TargetPlayerUid, packet.UidTwo);
        Assert.Equal("Target", packet.NameTwo);
        Assert.Equal(1002U, packet.ServerIdTwo);
        Assert.Equal(expectedTargetPoints, packet.PointsTwo);
        Assert.Equal(0U, packet.Reserved);
    }

    private static void AssertTeamPkQualifierScore(
        LegacyTeamPkMatchStatsPacket packet,
        uint expectedCasterPoints,
        uint expectedTargetPoints)
    {
        Assert.Equal(PlayerUid, packet.LeaderUidOne);
        Assert.Equal(1U, packet.TeamUidOne);
        Assert.Equal("Caster", packet.LeaderNameOne);
        Assert.Equal("CasterTeam", packet.TeamNameOne);
        Assert.Equal(expectedCasterPoints, packet.PointsOne);
        Assert.Equal(TargetPlayerUid, packet.LeaderUidTwo);
        Assert.Equal(2U, packet.TeamUidTwo);
        Assert.Equal("Target", packet.LeaderNameTwo);
        Assert.Equal("TargetTeam", packet.TeamNameTwo);
        Assert.Equal(expectedTargetPoints, packet.PointsTwo);
    }

    private sealed record PlayerReceivePkQualifierThunderResult(
        WorldSessionStore Sessions,
        ConquerTcpConnection CasterConnection,
        ConquerTcpConnection TargetConnection,
        List<byte[]> CasterSent,
        List<byte[]> TargetSent,
        string Log);
}
