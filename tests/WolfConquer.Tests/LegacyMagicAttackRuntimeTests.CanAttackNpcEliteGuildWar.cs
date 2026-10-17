using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int EliteGuildWarMapId = 6_521;
    private const int EliteGuildWarPoleLife = 250_000_000;
    private const int EliteGuildWarFurnituresPoleUid = 6_539;
    private const int EliteGuildWarFurnitures2PoleUid = 6_540;
    private const int EliteGuildWarFurnitures3PoleUid = 6_541;

    [Theory]
    [InlineData(EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures")]
    [InlineData(EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2")]
    [InlineData(EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3")]
    public async Task HandlerMagicAttackAgainstEliteGuildWarPoleWithActiveEnemyGuildSendsSpellAnimationAndDamagesSobNpc(
        int poleUid,
        string poleName,
        string furnitureName)
    {
        var result = await RunEliteGuildWarPoleMagicAttackAsync(
            poleUid,
            poleName,
            CreateEliteGuildWarPoleAttributes(furnitureName));

        AssertEliteGuildWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)poleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            EliteGuildWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures", null, "CasterGuild", EliteGuildWarPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures", 501, "EliteGuildWar", EliteGuildWarPoleLife, "Alive")]
    [InlineData("DeadProcess", EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures", 501, "CasterGuild", EliteGuildWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", EliteGuildWarFurnituresPoleUid, "EliteGuildWar", "Furnitures", 501, "CasterGuild", EliteGuildWarPoleLife, null)]
    [InlineData("NoGuild", EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2", null, "CasterGuild", EliteGuildWarPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2", 501, "EliteGuildWar2", EliteGuildWarPoleLife, "Alive")]
    [InlineData("DeadProcess", EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2", 501, "CasterGuild", EliteGuildWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", EliteGuildWarFurnitures2PoleUid, "EliteGuildWar2", "Furnitures2", 501, "CasterGuild", EliteGuildWarPoleLife, null)]
    [InlineData("NoGuild", EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3", null, "CasterGuild", EliteGuildWarPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3", 501, "EliteGuildWar3", EliteGuildWarPoleLife, "Alive")]
    [InlineData("DeadProcess", EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3", 501, "CasterGuild", EliteGuildWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", EliteGuildWarFurnitures3PoleUid, "EliteGuildWar3", "Furnitures3", 501, "CasterGuild", EliteGuildWarPoleLife, null)]
    public async Task HandlerMagicAttackAgainstEliteGuildWarPoleSourceGuardBlocksAttack(
        string scenario,
        int poleUid,
        string poleName,
        string furnitureName,
        int? guildId,
        string guildName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunEliteGuildWarPoleMagicAttackAsync(
            poleUid,
            poleName,
            CreateEliteGuildWarPoleAttributes(
                furnitureName,
                guildId,
                guildName,
                poleHitPoints,
                process));

        AssertEliteGuildWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(EliteGuildWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(
            ExpectedEliteGuildWarPoleFailureDiagnostic(
                scenario,
                result.SobNpc,
                furnitureName),
            result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<EliteGuildWarPoleAttackResult> RunEliteGuildWarPoleMagicAttackAsync(
        int poleUid,
        string poleName,
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEliteGuildWarPoleSobNpc(poleUid, poleName);
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
            mapId: EliteGuildWarMapId,
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
        return new EliteGuildWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertEliteGuildWarPoleManaSpent(EliteGuildWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateEliteGuildWarPoleSobNpc(int poleUid, string poleName)
    {
        return new SobNpc(
            Id: poleUid,
            Name: poleName,
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: EliteGuildWarMapId,
            X: 125,
            Y: 124,
            Life: EliteGuildWarPoleLife,
            MaxLife: EliteGuildWarPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{poleUid},{poleName},10,1137,{EliteGuildWarMapId},125,124,{EliteGuildWarPoleLife},{EliteGuildWarPoleLife},17,1",
            Fields:
            [
                poleUid.ToString(CultureInfo.InvariantCulture),
                poleName,
                "10",
                "1137",
                EliteGuildWarMapId.ToString(CultureInfo.InvariantCulture),
                "125",
                "124",
                EliteGuildWarPoleLife.ToString(CultureInfo.InvariantCulture),
                EliteGuildWarPoleLife.ToString(CultureInfo.InvariantCulture),
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateEliteGuildWarPoleAttributes(
        string furnitureName,
        int? guildId = 501,
        string guildName = "CasterGuild",
        int poleHitPoints = EliteGuildWarPoleLife,
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

        SetEliteGuildWarPoleHitPointsAttributePairs(attributes, furnitureName, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyEliteGuildWarProcess"] = process;
            attributes["EliteGuildWarProcess"] = process;
            attributes["RuntimeEliteGuildWarProcess"] = process;
        }

        return attributes;
    }

    private static void SetEliteGuildWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        string furnitureName,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        var scheduleName = EliteGuildWarScheduleName(furnitureName);
        attributes["Legacy" + scheduleName + "PoleHitPoints"] = value;
        attributes[scheduleName + "PoleHitPoints"] = value;
        attributes["LegacyEliteGuildWar" + furnitureName + "PoleHitPoints"] = value;
        attributes["EliteGuildWar" + furnitureName + "PoleHitPoints"] = value;
    }

    private static string EliteGuildWarScheduleName(string furnitureName)
    {
        return furnitureName switch
        {
            "Furnitures" => "EliteGuildWar",
            "Furnitures2" => "EliteGuildWar2",
            "Furnitures3" => "EliteGuildWar3",
            _ => throw new ArgumentOutOfRangeException(nameof(furnitureName), furnitureName, null),
        };
    }

    private static string ExpectedEliteGuildWarPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc,
        string furnitureName)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"EliteGuildWar pole uid={sobNpc.Id} because ";
        var sourcePole = $"MsgSchedules.EliteGuildWar.{furnitureName}[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + sourcePole + ".HitPoints is zero",
            "SamePoleGuildName" => prefix + "client.Player.MyGuild.GuildName matches " + sourcePole + ".Name",
            "DeadProcess" => prefix + "MsgSchedules.EliteGuildWar.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgSchedules.EliteGuildWar.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct EliteGuildWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
