using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int Guild6PoleWarPoleLife = 2_000_000_000;

    public static IEnumerable<object[]> Guild6PoleWarPoleCases()
    {
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar", "GuildPole[1]War", LegacySobNpcEventRules.Guild6PoleWarPoleUid, 80)];
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar2", "GuildPole[2]War", LegacySobNpcEventRules.Guild6PoleWar2PoleUid, 82)];
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar3", "GuildPole[3]War", LegacySobNpcEventRules.Guild6PoleWar3PoleUid, 84)];
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar4", "GuildPole[4]War", LegacySobNpcEventRules.Guild6PoleWar4PoleUid, 86)];
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar5", "GuildPole[5]War", LegacySobNpcEventRules.Guild6PoleWar5PoleUid, 88)];
        yield return [new Guild6PoleWarPoleCase("Guild6PoleWar6", "GuildPole[6]War", LegacySobNpcEventRules.Guild6PoleWar6PoleUid, 90)];
    }

    [Theory]
    [MemberData(nameof(Guild6PoleWarPoleCases))]
    public async Task HandlerMagicAttackAgainstGuild6PoleWarPoleWithActiveEnemyGuildSendsSpellAnimationAndDamagesSobNpc(
        Guild6PoleWarPoleCase pole)
    {
        var result = await RunGuild6PoleWarPoleMagicAttackAsync(
            pole,
            CreateGuild6PoleWarPoleAttributes(pole));

        AssertGuild6PoleWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)pole.Uid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            Guild6PoleWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [MemberData(nameof(Guild6PoleWarPoleCases))]
    public async Task HandlerMagicAttackAgainstGuild6PoleWarPoleWithoutGuildBlocksEverySourcePole(
        Guild6PoleWarPoleCase pole)
    {
        var result = await RunGuild6PoleWarPoleMagicAttackAsync(
            pole,
            CreateGuild6PoleWarPoleAttributes(pole, guildId: null));

        AssertGuild6PoleWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(Guild6PoleWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedGuild6PoleWarPoleFailureDiagnostic("NoGuild", pole, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    [Theory]
    [InlineData("ZeroPoleHitPoints", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", 501, "GuildPole[1]War", Guild6PoleWarPoleLife, "Alive")]
    [InlineData("DeadProcess", 501, "CasterGuild", Guild6PoleWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, "CasterGuild", Guild6PoleWarPoleLife, null)]
    public async Task HandlerMagicAttackAgainstGuild6PoleWarPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        string guildName,
        int poleHitPoints,
        string? process)
    {
        var pole = new Guild6PoleWarPoleCase(
            "Guild6PoleWar",
            "GuildPole[1]War",
            LegacySobNpcEventRules.Guild6PoleWarPoleUid,
            80);
        var result = await RunGuild6PoleWarPoleMagicAttackAsync(
            pole,
            CreateGuild6PoleWarPoleAttributes(
                pole,
                guildId,
                guildName,
                poleHitPoints,
                process));

        AssertGuild6PoleWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(Guild6PoleWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedGuild6PoleWarPoleFailureDiagnostic(scenario, pole, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<Guild6PoleWarPoleAttackResult> RunGuild6PoleWarPoleMagicAttackAsync(
        Guild6PoleWarPoleCase pole,
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateGuild6PoleWarPoleSobNpc(pole);
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
            mapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
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
        return new Guild6PoleWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertGuild6PoleWarPoleManaSpent(Guild6PoleWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateGuild6PoleWarPoleSobNpc(Guild6PoleWarPoleCase pole)
    {
        return new SobNpc(
            Id: pole.Uid,
            Name: pole.DisplayName,
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.Guild6PoleWarMapId,
            X: pole.X,
            Y: 84,
            Life: Guild6PoleWarPoleLife,
            MaxLife: Guild6PoleWarPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 0,
            RawLine: $"{pole.Uid},{pole.DisplayName},10,1137,10070,{pole.X},84,2000000000,2000000000,17,1",
            Fields:
            [
                pole.Uid.ToString(CultureInfo.InvariantCulture),
                pole.DisplayName,
                "10",
                "1137",
                "10070",
                pole.X.ToString(CultureInfo.InvariantCulture),
                "84",
                "2000000000",
                "2000000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateGuild6PoleWarPoleAttributes(
        Guild6PoleWarPoleCase pole,
        int? guildId = 501,
        string guildName = "CasterGuild",
        int poleHitPoints = Guild6PoleWarPoleLife,
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

        SetGuild6PoleWarPoleHitPointsAttributePairs(attributes, pole.ScheduleName, poleHitPoints);
        if (process is not null)
        {
            attributes["Legacy" + pole.ScheduleName + "Process"] = process;
            attributes[pole.ScheduleName + "Process"] = process;
            attributes["Runtime" + pole.ScheduleName + "Process"] = process;
        }

        return attributes;
    }

    private static void SetGuild6PoleWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        string scheduleName,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["Legacy" + scheduleName + "PoleHitPoints"] = value;
        attributes["Legacy" + scheduleName + "PoleHitpoints"] = value;
        attributes[scheduleName + "PoleHitPoints"] = value;
        attributes[scheduleName + "PoleHitpoints"] = value;
        attributes["Runtime" + scheduleName + "PoleHitPoints"] = value;
        attributes["Runtime" + scheduleName + "PoleHitpoints"] = value;
    }

    private static string ExpectedGuild6PoleWarPoleFailureDiagnostic(
        string scenario,
        Guild6PoleWarPoleCase pole,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{pole.ScheduleName} pole uid={sobNpc.Id} because ";
        var sourcePole = $"MsgSchedules.{pole.ScheduleName}.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + sourcePole + ".HitPoints is zero",
            "SamePoleGuildName" => prefix + "client.Player.MyGuild.GuildName matches " + sourcePole + ".Name",
            "DeadProcess" => prefix + $"MsgSchedules.{pole.ScheduleName}.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + $"MsgSchedules.{pole.ScheduleName}.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    public readonly record struct Guild6PoleWarPoleCase(
        string ScheduleName,
        string DisplayName,
        int Uid,
        int X);

    private readonly record struct Guild6PoleWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
