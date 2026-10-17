using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task PlayerReceiveQualifierRegularArenaMagicSendsScoreAndThunderDamageOnce()
    {
        var result = await RunRegularArenaPlayerReceiveQualifierThunderAsync(mapId: 700);

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

        AssertRegularArenaQualifierScore(
            Assert.Single(ParseArenaMatchScores(result.CasterSent)),
            expectedCasterDamage: ExpectedPlayerThunderDamage,
            expectedTargetDamage: 0);
        AssertRegularArenaQualifierScore(
            Assert.Single(ParseArenaMatchScores(result.TargetSent)),
            expectedCasterDamage: ExpectedPlayerThunderDamage,
            expectedTargetDamage: 0);
    }

    [Fact]
    public async Task PlayerReceiveQualifierRegularArenaLethalMagicDoesNotSendNonLethalScore()
    {
        var result = await RunRegularArenaPlayerReceiveQualifierThunderAsync(
            mapId: 700,
            targetMinHitPoints: 100);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);

        Assert.Empty(ParseArenaMatchScores(result.CasterSent));
        Assert.Empty(ParseArenaMatchScores(result.TargetSent));
    }

    [Fact]
    public async Task PlayerReceiveQualifierOutsideArenaMapDoesNotSendScore()
    {
        var result = await RunRegularArenaPlayerReceiveQualifierThunderAsync(mapId: 1_015);

        Assert.True(result.Sessions.TryGet(result.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        Assert.Empty(ParseArenaMatchScores(result.CasterSent));
        Assert.Empty(ParseArenaMatchScores(result.TargetSent));
    }

    private static async Task<PlayerReceiveQualifierThunderResult> RunRegularArenaPlayerReceiveQualifierThunderAsync(
        long mapId,
        long targetMinHitPoints = 5_000)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var arenaStates = new ArenaSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: mapId,
            attributes: CreateRegularArenaPlayerReceiveQualifierAttributes(mapId));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetMinHitPoints,
            mapId: mapId,
            attributes: CreateRegularArenaPlayerReceiveQualifierAttributes(mapId));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        ActivateRegularArenaMatch(arenaStates, casterConnection, targetConnection);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            ArenaStates: arenaStates,
            WorldData: worldData,
            Sessions: sessions);

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

        return new PlayerReceiveQualifierThunderResult(
            sessions,
            casterConnection,
            targetConnection,
            casterSent,
            targetSent);
    }

    private static Dictionary<string, string>? CreateRegularArenaPlayerReceiveQualifierAttributes(long mapId)
    {
        return mapId == 700
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DynamicID"] = "42",
            }
            : null;
    }

    private static void ActivateRegularArenaMatch(
        ArenaSessionStateStore arenaStates,
        ConquerTcpConnection casterConnection,
        ConquerTcpConnection targetConnection)
    {
        Assert.False(arenaStates.RegisterWaiting(casterConnection, arenaPoints: 4_500, out _));
        Assert.True(arenaStates.RegisterWaiting(targetConnection, arenaPoints: 4_300, out _));
        Assert.False(arenaStates.TryAcceptCountdown(casterConnection, out _));
        Assert.True(arenaStates.TryAcceptCountdown(targetConnection, out var pair));
        Assert.Same(casterConnection, pair.First);
        Assert.Same(targetConnection, pair.Second);
    }

    private static List<LegacyArenaMatchScorePacket> ParseArenaMatchScores(IEnumerable<byte[]> frames)
    {
        var scores = new List<LegacyArenaMatchScorePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyArenaMatchScorePacket.TryParse(ReadFrame(bytes), out var score))
            {
                scores.Add(score);
            }
        }

        return scores;
    }

    private static void AssertRegularArenaQualifierScore(
        LegacyArenaMatchScorePacket score,
        uint expectedCasterDamage,
        uint expectedTargetDamage)
    {
        Assert.Equal(PlayerUid, score.UidOne);
        Assert.Equal(0U, score.ServerIdOne);
        Assert.Equal("Caster", score.NameOne);
        Assert.Equal(expectedCasterDamage, score.DamageOne);
        Assert.Equal(TargetPlayerUid, score.UidTwo);
        Assert.Equal(0U, score.ServerIdTwo);
        Assert.Equal("Target", score.NameTwo);
        Assert.Equal(expectedTargetDamage, score.DamageTwo);
        Assert.Equal(LegacyArenaMatchScorePacket.LegacyResult, score.Result);
    }

    private sealed record PlayerReceiveQualifierThunderResult(
        WorldSessionStore Sessions,
        ConquerTcpConnection CasterConnection,
        ConquerTcpConnection TargetConnection,
        List<byte[]> CasterSent,
        List<byte[]> TargetSent);
}
