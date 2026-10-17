using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int EmperorWarPoleLife = 50_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstEmperorWarPoleWithActiveDifferentPlayerNameSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunEmperorWarPoleMagicAttackAsync(CreateEmperorWarPoleAttributes());

        AssertEmperorWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.EmperorWarPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            EmperorWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", null, "Caster", EmperorWarPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, "Caster", 0, "Alive")]
    [InlineData("SamePlayerName", 501, "Emperrors", EmperorWarPoleLife, "Alive")]
    [InlineData("DeadProcess", 501, "Caster", EmperorWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, "Caster", EmperorWarPoleLife, null)]
    public async Task HandlerMagicAttackAgainstEmperorWarPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        string casterName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunEmperorWarPoleMagicAttackAsync(
            CreateEmperorWarPoleAttributes(
                guildId,
                poleHitPoints,
                process),
            casterName);

        AssertEmperorWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(EmperorWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedEmperorWarPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<EmperorWarPoleAttackResult> RunEmperorWarPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster")
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEmperorWarPoleSobNpc();
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
            mapId: LegacySobNpcEventRules.EmperorWarMapId,
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
        return new EmperorWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertEmperorWarPoleManaSpent(EmperorWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateEmperorWarPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.EmperorWarPoleUid,
            Name: "Emperrors",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.EmperorWarMapId,
            X: 119,
            Y: 120,
            Life: EmperorWarPoleLife,
            MaxLife: EmperorWarPoleLife,
            Unknown9: 17,
            Unknown10: null,
            LocalizedName: null,
            SourceLine: 76,
            RawLine: "6505,Emperrors,10,1137,10702,119,120,50000000,50000000,17,",
            Fields:
            [
                "6505",
                "Emperrors",
                "10",
                "1137",
                "10702",
                "119",
                "120",
                "50000000",
                "50000000",
                "17",
                "",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateEmperorWarPoleAttributes(
        int? guildId = 501,
        int poleHitPoints = EmperorWarPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guildId is { } id)
        {
            var guildIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["GuildID"] = guildIdText;
            attributes["GuildId"] = guildIdText;
        }

        SetEmperorWarPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgEmperorWarProcess"] = process;
            attributes["MsgEmperorWarProcess"] = process;
            attributes["RuntimeMsgEmperorWarProcess"] = process;
            attributes["LegacyEmperorWarProcess"] = process;
            attributes["EmperorWarProcess"] = process;
            attributes["RuntimeEmperorWarProcess"] = process;
        }

        return attributes;
    }

    private static void SetEmperorWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgEmperorWarHitPoints"] = value;
        attributes["LegacyMsgEmperorWarHitpoints"] = value;
        attributes["MsgEmperorWarHitPoints"] = value;
        attributes["MsgEmperorWarHitpoints"] = value;
        attributes["LegacyEmperorWarHitPoints"] = value;
        attributes["LegacyEmperorWarHitpoints"] = value;
        attributes["EmperorWarHitPoints"] = value;
        attributes["EmperorWarHitpoints"] = value;
    }

    private static string ExpectedEmperorWarPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgEmperorWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgEmperorWar.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePlayerName" => prefix + "client.Player.Name matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgEmperorWar.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgEmperorWar.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct EmperorWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
