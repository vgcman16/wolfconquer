using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int NobilityPoleMapId = 11_140;
    private const int NobilityPoleLife = 50_000;
    private const int NobilityPoleKingUid = 22_340;
    private const int NobilityPolePrinceUid = 22_341;
    private const int NobilityPoleDukeUid = 22_342;
    private const int NobilityPoleEarlUid = 22_343;
    private const int NobilityRankKing = 12;
    private const int NobilityRankPrince = 9;
    private const int NobilityRankDuke = 7;
    private const int NobilityRankEarl = 5;

    [Theory]
    [InlineData(NobilityPoleKingUid, "MsgNobilityPole", NobilityRankKing)]
    [InlineData(NobilityPolePrinceUid, "MsgNobilityPole1", NobilityRankPrince)]
    [InlineData(NobilityPoleDukeUid, "MsgNobilityPole2", NobilityRankDuke)]
    [InlineData(NobilityPoleEarlUid, "MsgNobilityPole3", NobilityRankEarl)]
    public async Task HandlerMagicAttackAgainstNobilityPoleWithActiveMatchingRankSendsSpellAnimationAndDamagesSobNpc(
        int poleUid,
        string scheduleName,
        int requiredRank)
    {
        var result = await RunNobilityPoleMagicAttackAsync(
            poleUid,
            scheduleName,
            CreateNobilityPoleAttributes(scheduleName, requiredRank));

        AssertNobilityPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)poleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            NobilityPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData(NobilityPoleKingUid, "MsgNobilityPole", NobilityRankKing, NobilityRankPrince, "King")]
    [InlineData(NobilityPolePrinceUid, "MsgNobilityPole1", NobilityRankPrince, NobilityRankKing, "Prince")]
    [InlineData(NobilityPoleDukeUid, "MsgNobilityPole2", NobilityRankDuke, NobilityRankEarl, "Duke")]
    [InlineData(NobilityPoleEarlUid, "MsgNobilityPole3", NobilityRankEarl, NobilityRankDuke, "Earl")]
    public async Task HandlerMagicAttackAgainstNobilityPoleBlocksWhenRankDoesNotMatchRequiredRank(
        int poleUid,
        string scheduleName,
        int requiredRank,
        int casterRank,
        string requiredRankName)
    {
        Assert.NotEqual(requiredRank, casterRank);

        var result = await RunNobilityPoleMagicAttackAsync(
            poleUid,
            scheduleName,
            CreateNobilityPoleAttributes(scheduleName, casterRank));

        AssertNobilityPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(NobilityPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(
            ExpectedNobilityPoleFailureDiagnostic(
                result.SobNpc,
                scheduleName,
                $"caster NobilityRank={casterRank} is not {requiredRankName}"),
            result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    [Theory]
    [InlineData("ZeroPoleHitPoints", NobilityRankKing, 0, "Alive", "Caster", "EnemyKingPole")]
    [InlineData("SameOwnerName", NobilityRankKing, NobilityPoleLife, "Alive", "Caster", "Caster")]
    [InlineData("DeadProcess", NobilityRankKing, NobilityPoleLife, "Dead", "Caster", "EnemyKingPole")]
    [InlineData("MissingProcessDefaultsDead", NobilityRankKing, NobilityPoleLife, null, "Caster", "EnemyKingPole")]
    public async Task HandlerMagicAttackAgainstNobilityPoleSourceGuardBlocksAttack(
        string scenario,
        int casterRank,
        int poleHitPoints,
        string? process,
        string casterName,
        string poleName)
    {
        const int poleUid = NobilityPoleKingUid;
        const string scheduleName = "MsgNobilityPole";

        var result = await RunNobilityPoleMagicAttackAsync(
            poleUid,
            scheduleName,
            CreateNobilityPoleAttributes(
                scheduleName,
                casterRank,
                poleHitPoints,
                process),
            casterName,
            poleName);

        AssertNobilityPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(NobilityPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedNobilityPoleFailureDiagnostic(scenario, result.SobNpc, scheduleName), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<NobilityPoleAttackResult> RunNobilityPoleMagicAttackAsync(
        int poleUid,
        string scheduleName,
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster",
        string poleName = "EnemyNobilityPole")
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateNobilityPoleSobNpc(poleUid, poleName);
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
            mapId: NobilityPoleMapId,
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
        return new NobilityPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertNobilityPoleManaSpent(NobilityPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateNobilityPoleSobNpc(int poleUid, string poleName)
    {
        return new SobNpc(
            Id: poleUid,
            Name: poleName,
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: NobilityPoleMapId,
            X: 85,
            Y: 70,
            Life: NobilityPoleLife,
            MaxLife: NobilityPoleLife,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{poleUid},{poleName},10,1137,{NobilityPoleMapId},85,70,{NobilityPoleLife},{NobilityPoleLife},17,0",
            Fields:
            [
                poleUid.ToString(CultureInfo.InvariantCulture),
                poleName,
                "10",
                "1137",
                NobilityPoleMapId.ToString(CultureInfo.InvariantCulture),
                "85",
                "70",
                NobilityPoleLife.ToString(CultureInfo.InvariantCulture),
                NobilityPoleLife.ToString(CultureInfo.InvariantCulture),
                "17",
                "0",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateNobilityPoleAttributes(
        string scheduleName,
        int nobilityRank,
        int poleHitPoints = NobilityPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NobilityRank"] = nobilityRank.ToString(CultureInfo.InvariantCulture),
        };
        SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "PoleHitPoints", poleHitPoints);
        if (process is not null)
        {
            SetNobilityPoleScheduleAttributePair(attributes, scheduleName, "Process", process);
        }

        return attributes;
    }

    private static void SetNobilityPoleScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        int value)
    {
        SetNobilityPoleScheduleAttributePair(
            attributes,
            scheduleName,
            suffix,
            value.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetNobilityPoleScheduleAttributePair(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
    }

    private static string ExpectedNobilityPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc,
        string scheduleName)
    {
        return scenario switch
        {
            "ZeroPoleHitPoints" => ExpectedNobilityPoleFailureDiagnostic(
                sobNpc,
                scheduleName,
                $"MsgSchedules.{scheduleName}.Furnitures[Pole].HitPoints is zero"),
            "SameOwnerName" => ExpectedNobilityPoleFailureDiagnostic(
                sobNpc,
                scheduleName,
                "pole owner name matches caster name"),
            "DeadProcess" => ExpectedNobilityPoleFailureDiagnostic(
                sobNpc,
                scheduleName,
                $"MsgSchedules.{scheduleName}.Proces is Dead"),
            "MissingProcessDefaultsDead" => ExpectedNobilityPoleFailureDiagnostic(
                sobNpc,
                scheduleName,
                $"MsgSchedules.{scheduleName}.Proces is Dead"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private static string ExpectedNobilityPoleFailureDiagnostic(
        SobNpc sobNpc,
        string scheduleName,
        string reason)
    {
        return
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because {reason}";
    }

    private readonly record struct NobilityPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
