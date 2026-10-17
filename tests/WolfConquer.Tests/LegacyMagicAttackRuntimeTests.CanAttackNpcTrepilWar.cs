using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int TrepilWarPoleLife = 250_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstTrepilWarPoleWithActiveEnemyGuildSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunTrepilWarPoleMagicAttackAsync(CreateTrepilWarPoleAttributes());

        AssertTrepilWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.TrepilWarPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            TrepilWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", null, "CasterGuild", TrepilWarPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", 501, "Trepile", TrepilWarPoleLife, "Alive")]
    [InlineData("DeadProcess", 501, "CasterGuild", TrepilWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, "CasterGuild", TrepilWarPoleLife, null)]
    public async Task HandlerMagicAttackAgainstTrepilWarPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        string guildName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunTrepilWarPoleMagicAttackAsync(
            CreateTrepilWarPoleAttributes(
                guildId,
                guildName,
                poleHitPoints,
                process));

        AssertTrepilWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(TrepilWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedTrepilWarPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<TrepilWarPoleAttackResult> RunTrepilWarPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrepilWarPoleSobNpc();
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
            mapId: LegacySobNpcEventRules.TrepilWarMapId,
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
        return new TrepilWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertTrepilWarPoleManaSpent(TrepilWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateTrepilWarPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.TrepilWarPoleUid,
            Name: "Trepile",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.TrepilWarMapId,
            X: 221,
            Y: 221,
            Life: TrepilWarPoleLife,
            MaxLife: TrepilWarPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 78,
            RawLine: "65046,Trepile,10,1137,3838,221,221,250000000,250000000,17,1",
            Fields:
            [
                "65046",
                "Trepile",
                "10",
                "1137",
                "3838",
                "221",
                "221",
                "250000000",
                "250000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateTrepilWarPoleAttributes(
        int? guildId = 501,
        string guildName = "CasterGuild",
        int poleHitPoints = TrepilWarPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guildId is { } id)
        {
            var guildIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["GuildID"] = guildIdText;
            attributes["GuildId"] = guildIdText;
            attributes["GuildName"] = guildName;
            attributes["LegacyGuildName"] = guildName;
        }

        SetTrepilWarPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgTrepilWarProcess"] = process;
            attributes["MsgTrepilWarProcess"] = process;
            attributes["RuntimeMsgTrepilWarProcess"] = process;
            attributes["LegacyTrepilWarProcess"] = process;
            attributes["TrepilWarProcess"] = process;
            attributes["RuntimeTrepilWarProcess"] = process;
        }

        return attributes;
    }

    private static void SetTrepilWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgTrepilWarPoleHitPoints"] = value;
        attributes["LegacyMsgTrepilWarPoleHitpoints"] = value;
        attributes["MsgTrepilWarPoleHitPoints"] = value;
        attributes["MsgTrepilWarPoleHitpoints"] = value;
        attributes["LegacyTrepilWarPoleHitPoints"] = value;
        attributes["LegacyTrepilWarPoleHitpoints"] = value;
        attributes["TrepilWarPoleHitPoints"] = value;
        attributes["TrepilWarPoleHitpoints"] = value;
    }

    private static string ExpectedTrepilWarPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgTrepilWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.MsgTrepilWar.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePoleGuildName" => prefix + "client.Player.MyGuild.GuildName matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgSchedules.MsgTrepilWar.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgSchedules.MsgTrepilWar.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct TrepilWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
