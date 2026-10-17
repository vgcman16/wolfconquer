using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int TopPoleLife = 50_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstTopPoleWithActiveEnemyPlayerNameSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunTopPoleMagicAttackAsync(CreateTopPoleAttributes());

        AssertTopPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.TopPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            TopPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", null, "Caster", TopPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, "Caster", 0, "Alive")]
    [InlineData("SamePlayerName", 501, "TestSrv", TopPoleLife, "Alive")]
    [InlineData("DeadProcess", 501, "Caster", TopPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, "Caster", TopPoleLife, null)]
    public async Task HandlerMagicAttackAgainstTopPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        string casterName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunTopPoleMagicAttackAsync(
            CreateTopPoleAttributes(
                guildId,
                poleHitPoints,
                process),
            casterName);

        AssertTopPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(TopPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedTopPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<TopPoleAttackResult> RunTopPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster")
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTopPoleSobNpc();
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
            name: casterName,
            mapId: LegacySobNpcEventRules.TopPoleMapId,
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
        return new TopPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertTopPoleManaSpent(TopPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateTopPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.TopPoleUid,
            Name: "TestSrv",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.TopPoleMapId,
            X: 70,
            Y: 67,
            Life: TopPoleLife,
            MaxLife: TopPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 77,
            RawLine: "65056,TestSrv,10,1137,1737,70,67,50000000,50000000,17,1",
            Fields:
            [
                "65056",
                "TestSrv",
                "10",
                "1137",
                "1737",
                "70",
                "67",
                "50000000",
                "50000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateTopPoleAttributes(
        int? guildId = 501,
        int poleHitPoints = TopPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guildId is { } id)
        {
            var guildIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["GuildID"] = guildIdText;
            attributes["GuildId"] = guildIdText;
        }

        SetTopPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgTopPoleProcess"] = process;
            attributes["MsgTopPoleProcess"] = process;
            attributes["RuntimeMsgTopPoleProcess"] = process;
            attributes["LegacyTopPoleProcess"] = process;
            attributes["TopPoleProcess"] = process;
            attributes["RuntimeTopPoleProcess"] = process;
        }

        return attributes;
    }

    private static void SetTopPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgTopPoleHitPoints"] = value;
        attributes["LegacyMsgTopPoleHitpoints"] = value;
        attributes["MsgTopPoleHitPoints"] = value;
        attributes["MsgTopPoleHitpoints"] = value;
        attributes["LegacyTopPoleHitPoints"] = value;
        attributes["LegacyTopPoleHitpoints"] = value;
        attributes["TopPoleHitPoints"] = value;
        attributes["TopPoleHitpoints"] = value;
    }

    private static string ExpectedTopPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgTopPole pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgTopPole.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePlayerName" => prefix + "client.Player.Name matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgTopPole.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgTopPole.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct TopPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
