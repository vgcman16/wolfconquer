using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PoleDominationPoleLife = 50_000;
    private const int PoleDominationMapId = 11_083;

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.PoleDominationPoleUid,
        "PoleDomination")]
    [InlineData(
        LegacySobNpcEventRules.PoleDominationBirdIslandPoleUid,
        "PoleDominationBI")]
    [InlineData(
        LegacySobNpcEventRules.PoleDominationDesertCityPoleUid,
        "PoleDominationDC")]
    [InlineData(
        LegacySobNpcEventRules.PoleDominationPhoenixCastlePoleUid,
        "PoleDominationPC")]
    public async Task HandlerMagicAttackAgainstPoleDominationSobGateWithActiveEnemyWinnerSendsSpellAnimationAndDamagesSobNpc(
        int poleUid,
        string scheduleName)
    {
        var result = await RunPoleDominationPoleMagicAttackAsync(
            poleUid,
            scheduleName,
            CreatePoleDominationPoleAttributes(scheduleName));

        AssertPoleDominationPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)poleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            PoleDominationPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", 0, PoleDominationPoleLife, 602, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, 0, 602, "Alive")]
    [InlineData("SameWinnerGuild", 501, PoleDominationPoleLife, 501, "Alive")]
    [InlineData("DeadProcess", 501, PoleDominationPoleLife, 602, "Dead")]
    [InlineData("IdleProcess", 501, PoleDominationPoleLife, 602, "Idle")]
    public async Task HandlerMagicAttackAgainstPoleDominationSobGateSourceGuardBlocksAttack(
        string scenario,
        int guildId,
        int poleHitPoints,
        int winnerGuildId,
        string process)
    {
        const int poleUid = LegacySobNpcEventRules.PoleDominationPoleUid;
        const string scheduleName = "PoleDomination";

        var result = await RunPoleDominationPoleMagicAttackAsync(
            poleUid,
            scheduleName,
            CreatePoleDominationPoleAttributes(
                scheduleName,
                guildId,
                poleHitPoints,
                winnerGuildId,
                process));

        AssertPoleDominationPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(PoleDominationPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedPoleDominationPoleFailureDiagnostic(scenario, result.SobNpc, scheduleName), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<PoleDominationPoleAttackResult> RunPoleDominationPoleMagicAttackAsync(
        int poleUid,
        string scheduleName,
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreatePoleDominationPoleSobNpc(poleUid, scheduleName);
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
            mapId: PoleDominationMapId,
            x: 84,
            y: 70,
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
        return new PoleDominationPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertPoleDominationPoleManaSpent(PoleDominationPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreatePoleDominationPoleSobNpc(int poleUid, string scheduleName)
    {
        return new SobNpc(
            Id: poleUid,
            Name: scheduleName + "Pole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: PoleDominationMapId,
            X: 85,
            Y: 70,
            Life: PoleDominationPoleLife,
            MaxLife: PoleDominationPoleLife,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{poleUid},{scheduleName}Pole,10,1137,{PoleDominationMapId},85,70,{PoleDominationPoleLife},{PoleDominationPoleLife},17,0",
            Fields:
            [
                poleUid.ToString(CultureInfo.InvariantCulture),
                scheduleName + "Pole",
                "10",
                "1137",
                PoleDominationMapId.ToString(CultureInfo.InvariantCulture),
                "85",
                "70",
                PoleDominationPoleLife.ToString(CultureInfo.InvariantCulture),
                PoleDominationPoleLife.ToString(CultureInfo.InvariantCulture),
                "17",
                "0",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreatePoleDominationPoleAttributes(
        string scheduleName,
        int guildId = 501,
        int poleHitPoints = PoleDominationPoleLife,
        int winnerGuildId = 602,
        string process = "Alive")
    {
        var attributes = new Dictionary<string, string>
        {
            ["GuildID"] = guildId.ToString(CultureInfo.InvariantCulture),
            ["GuildId"] = guildId.ToString(CultureInfo.InvariantCulture),
        };
        SetPoleDominationScheduleAttributePair(attributes, scheduleName, "PoleHitPoints", poleHitPoints);
        SetPoleDominationScheduleAttributePair(attributes, scheduleName, "WinnerGuildID", winnerGuildId);
        SetPoleDominationScheduleAttributePair(attributes, scheduleName, "Process", process);
        return attributes;
    }

    private static void SetPoleDominationScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        int value)
    {
        SetPoleDominationScheduleAttributePair(
            attributes,
            scheduleName,
            suffix,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetPoleDominationScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
    }

    private static string ExpectedPoleDominationPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc,
        string scheduleName)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because ";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + $"MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero",
            "SameWinnerGuild" => prefix + "winnerGuildId=501 matches caster GuildID",
            "DeadProcess" => prefix + $"MsgSchedules.{scheduleName}.Proces is Dead",
            "IdleProcess" => prefix + $"MsgSchedules.{scheduleName}.Proces is Idle",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct PoleDominationPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
