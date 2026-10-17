using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int UnionWarPoleMapId = 1_002;
    private const ulong UnionWarCasterUnionId = 42;
    private const ulong UnionWarCasterGuildId = 77;
    private const ulong UnionWarEnemyWinnerUnionId = 99;
    private const int UnionWarPoleHitPoints = 50_000;

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleAllowsDamageWhenCasterIsInActiveWarAndWinnerIsOtherUnion()
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: UnionWarCasterUnionId,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: UnionWarPoleHitPoints,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: "Alive"),
            expectedDamage: true,
            expectedDiagnostic: "");
    }

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenCasterHasNoUnion()
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: null,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: UnionWarPoleHitPoints,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: "Alive"),
            expectedDamage: false,
            expectedDiagnostic: "UnionWar pole uid=22348 because caster has no union");
    }

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenPoleHitPointsAreZero()
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: UnionWarCasterUnionId,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: 0,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: "Alive"),
            expectedDamage: false,
            expectedDiagnostic: "UnionWar pole uid=22348 because MsgSchedules.UnionWar.Furnitures[Pole].HitPoints is zero");
    }

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenCasterHasNoGuild()
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: UnionWarCasterUnionId,
                casterGuildId: null,
                poleHitPoints: UnionWarPoleHitPoints,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: "Alive"),
            expectedDamage: false,
            expectedDiagnostic: "UnionWar pole uid=22348 because caster has no guild");
    }

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenWinnerUnionMatchesCasterGuildUnion()
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: UnionWarCasterUnionId,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: UnionWarPoleHitPoints,
                winnerUnionId: UnionWarCasterUnionId,
                process: "Alive"),
            expectedDamage: false,
            expectedDiagnostic: "UnionWar pole uid=22348 because winnerUnionId=42 matches caster guild UnionID");
    }

    [Theory]
    [InlineData("Dead", "UnionWar pole uid=22348 because MsgSchedules.UnionWar.Proces is Dead")]
    [InlineData("Idle", "UnionWar pole uid=22348 because MsgSchedules.UnionWar.Proces is Idle")]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenUnionWarProcessIsDeadOrIdle(
        string process,
        string expectedDiagnostic)
    {
        await RunUnionWarPoleGateProofAsync(
            CreateUnionWarAttributes(
                casterUnionId: UnionWarCasterUnionId,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: UnionWarPoleHitPoints,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: process),
            expectedDamage: false,
            expectedDiagnostic: expectedDiagnostic);
    }

    [Fact]
    public async Task HandlerThunderAgainstUnionWarPoleBlocksWhenUnionWarProcessIsMissingBecauseSourceDefaultsDead()
    {
        var attributes = CreateUnionWarAttributes(
            casterUnionId: UnionWarCasterUnionId,
            casterGuildId: UnionWarCasterGuildId,
            poleHitPoints: UnionWarPoleHitPoints,
            winnerUnionId: UnionWarEnemyWinnerUnionId,
            process: "");
        attributes.Remove("LegacyUnionWarProcess");
        attributes.Remove("UnionWarProcess");

        await RunUnionWarPoleGateProofAsync(
            attributes,
            expectedDamage: false,
            expectedDiagnostic: "UnionWar pole uid=22348 because MsgSchedules.UnionWar.Proces is Dead");
    }

    private static async Task RunUnionWarPoleGateProofAsync(
        IReadOnlyDictionary<string, string> attributes,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateUnionWarPoleSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var spells = ParseSpellAnimations(sent);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        var output = log.ToString();
        if (expectedDamage)
        {
            var spell = Assert.Single(spells);
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(sobNpc.X, spell.X);
            Assert.Equal(sobNpc.Y, spell.Y);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)LegacySobNpcEventRules.UnionWarPoleUid), target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(sobNpc.Life - (int)target.Damage, visibleSobNpc.Life);
            Assert.Contains("WorldServer handled legacy player-SOB magic", output);
            Assert.DoesNotContain("source CanAttackNpc.Verified rejected", output);
            return;
        }

        Assert.Empty(spells);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Contains("source CanAttackNpc.Verified rejected", output);
        Assert.Contains(expectedDiagnostic, output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    private static Dictionary<string, string> CreateUnionWarAttributes(
        ulong? casterUnionId,
        ulong? casterGuildId,
        int poleHitPoints,
        ulong winnerUnionId,
        string process)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyUnionWarPoleHitPoints"] = poleHitPoints.ToString(CultureInfo.InvariantCulture),
            ["UnionWarPoleHitPoints"] = poleHitPoints.ToString(CultureInfo.InvariantCulture),
            ["LegacyUnionWarWinnerUnionID"] = winnerUnionId.ToString(CultureInfo.InvariantCulture),
            ["UnionWarWinnerUnionID"] = winnerUnionId.ToString(CultureInfo.InvariantCulture),
            ["LegacyUnionWarProcess"] = process,
            ["UnionWarProcess"] = process,
        };

        if (casterUnionId is { } unionId)
        {
            attributes["UnionID"] = unionId.ToString(CultureInfo.InvariantCulture);
            attributes["UnionUID"] = unionId.ToString(CultureInfo.InvariantCulture);
        }

        if (casterGuildId is { } guildId)
        {
            attributes["GuildID"] = guildId.ToString(CultureInfo.InvariantCulture);
            attributes["GuildId"] = guildId.ToString(CultureInfo.InvariantCulture);
        }

        return attributes;
    }

    private static SobNpc CreateUnionWarPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.UnionWarPoleUid,
            Name: "UnionWarPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: UnionWarPoleMapId,
            X: 85,
            Y: 70,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 12,
            RawLine: "22348,UnionWarPole,10,1137,1002,85,70,50000,50000,17,0",
            Fields:
            [
                "22348",
                "UnionWarPole",
                "10",
                "1137",
                "1002",
                "85",
                "70",
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }
}
