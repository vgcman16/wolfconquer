using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ClanDominationPoleLife = 50_000;

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.TwinCityClanDominationPoleUid,
        1_002,
        "TCClanDomination")]
    [InlineData(
        LegacySobNpcEventRules.PhoenixCastleClanDominationPoleUid,
        1_011,
        "PHClanDomination")]
    [InlineData(
        LegacySobNpcEventRules.ApeCityClanDominationPoleUid,
        1_020,
        "APClanDomination")]
    [InlineData(
        LegacySobNpcEventRules.DesertCityClanDominationPoleUid,
        1_000,
        "DCClanDomination")]
    [InlineData(
        LegacySobNpcEventRules.BirdIslandClanDominationPoleUid,
        1_015,
        "BRClanDomination")]
    public async Task HandlerMagicAttackAgainstClanDominationPoleWithActiveEnemyWinnerSendsSpellAnimationAndDamagesSobNpc(
        int poleUid,
        int mapId,
        string scheduleName)
    {
        var result = await RunClanDominationPoleMagicAttackAsync(
            poleUid,
            mapId,
            scheduleName,
            CreateClanDominationPoleAttributes(scheduleName));

        AssertClanDominationPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)poleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            ClanDominationPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoClan", 0, ClanDominationPoleLife, 602, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, 0, 602, "Alive")]
    [InlineData("SameWinnerClan", 501, ClanDominationPoleLife, 501, "Alive")]
    [InlineData("DeadProcess", 501, ClanDominationPoleLife, 602, "Dead")]
    [InlineData("IdleProcess", 501, ClanDominationPoleLife, 602, "Idle")]
    public async Task HandlerMagicAttackAgainstClanDominationPoleSourceGuardBlocksAttack(
        string scenario,
        int clanId,
        int poleHitPoints,
        int winnerClanId,
        string process)
    {
        const int poleUid = LegacySobNpcEventRules.TwinCityClanDominationPoleUid;
        const int mapId = 1_002;
        const string scheduleName = "TCClanDomination";

        var result = await RunClanDominationPoleMagicAttackAsync(
            poleUid,
            mapId,
            scheduleName,
            CreateClanDominationPoleAttributes(
                scheduleName,
                clanId,
                poleHitPoints,
                winnerClanId,
                process));

        AssertClanDominationPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(ClanDominationPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedClanDominationPoleFailureDiagnostic(scenario, result.SobNpc, scheduleName), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<ClanDominationPoleAttackResult> RunClanDominationPoleMagicAttackAsync(
        int poleUid,
        int mapId,
        string scheduleName,
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateClanDominationPoleSobNpc(poleUid, mapId, scheduleName);
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
            mapId: mapId,
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
        return new ClanDominationPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertClanDominationPoleManaSpent(ClanDominationPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateClanDominationPoleSobNpc(int poleUid, int mapId, string scheduleName)
    {
        return new SobNpc(
            Id: poleUid,
            Name: scheduleName + "Pole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: mapId,
            X: 85,
            Y: 70,
            Life: ClanDominationPoleLife,
            MaxLife: ClanDominationPoleLife,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{poleUid},{scheduleName}Pole,10,1137,{mapId},85,70,{ClanDominationPoleLife},{ClanDominationPoleLife},17,0",
            Fields:
            [
                poleUid.ToString(CultureInfo.InvariantCulture),
                scheduleName + "Pole",
                "10",
                "1137",
                mapId.ToString(CultureInfo.InvariantCulture),
                "85",
                "70",
                ClanDominationPoleLife.ToString(CultureInfo.InvariantCulture),
                ClanDominationPoleLife.ToString(CultureInfo.InvariantCulture),
                "17",
                "0",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateClanDominationPoleAttributes(
        string scheduleName,
        int clanId = 501,
        int poleHitPoints = ClanDominationPoleLife,
        int winnerClanId = 602,
        string process = "Alive")
    {
        var attributes = new Dictionary<string, string>
        {
            ["ClanID"] = clanId.ToString(CultureInfo.InvariantCulture),
            ["ClanUID"] = clanId.ToString(CultureInfo.InvariantCulture),
        };
        SetClanDominationScheduleAttributePair(attributes, scheduleName, "PoleHitPoints", poleHitPoints);
        SetClanDominationScheduleAttributePair(attributes, scheduleName, "WinnerClanID", winnerClanId);
        SetClanDominationScheduleAttributePair(attributes, scheduleName, "Process", process);
        return attributes;
    }

    private static void SetClanDominationScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        int value)
    {
        SetClanDominationScheduleAttributePair(
            attributes,
            scheduleName,
            suffix,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetClanDominationScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
    }

    private static string ExpectedClanDominationPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc,
        string scheduleName)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because ";
        return scenario switch
        {
            "NoClan" => prefix + "caster has no clan",
            "ZeroPoleHitPoints" => prefix + $"MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero",
            "SameWinnerClan" => prefix + "winnerClanId=501 matches caster ClanUID",
            "DeadProcess" => prefix + $"MsgSchedules.{scheduleName}.Proces is Dead",
            "IdleProcess" => prefix + $"MsgSchedules.{scheduleName}.Proces is Idle",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct ClanDominationPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
