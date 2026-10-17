using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ClassPoleWarPoleLife = 50_000_000;

    public static IEnumerable<object[]> ClassPoleWarPoleCases()
    {
        yield return new object[] { "MsgWarriorGod", LegacySobNpcEventRules.ClassPoleWarWarriorGodPoleUid, 2_000, "WarriorGod" };
        yield return new object[] { "MsgArcherMaster", LegacySobNpcEventRules.ClassPoleWarArcherMasterPoleUid, 4_000, "ArcherMaster" };
        yield return new object[] { "MsgNinjaMaster", LegacySobNpcEventRules.ClassPoleWarNinjaMasterPoleUid, 5_000, "NinjaMaster" };
        yield return new object[] { "MsgTrojanMaster", LegacySobNpcEventRules.ClassPoleWarTrojanMasterPoleUid, 1_000, "TrojanMaster" };
        yield return new object[] { "MsgTaoistMaster", LegacySobNpcEventRules.ClassPoleWarTaoistMasterPoleUid, 13_002, "TaoistMaster" };
        yield return new object[] { "MsgFireMaster", LegacySobNpcEventRules.ClassPoleWarFireMasterPoleUid, 14_002, "FireMaster" };
        yield return new object[] { "MsgPirateMaster", LegacySobNpcEventRules.ClassPoleWarPirateMasterPoleUid, 7_000, "PirateMaster" };
        yield return new object[] { "MsgKungfuKing", LegacySobNpcEventRules.ClassPoleWarKungfuKingPoleUid, 8_000, "KungfuKing" };
        yield return new object[] { "MsgWindWalker", LegacySobNpcEventRules.ClassPoleWarWindWalkerPoleUid, 16_000, "WindWalker" };
        yield return new object[] { "MsgThnderStrike", LegacySobNpcEventRules.ClassPoleWarThnderStrikePoleUid, 9_000, "ThnderStrike" };
        yield return new object[] { "MsgClassPolaWar", LegacySobNpcEventRules.ClassPoleWarClassPolaWarPoleUid, 1_000, "ClassPolaWar" };
        yield return new object[] { "MsgMonkMaster", LegacySobNpcEventRules.ClassPoleWarMonkMasterPoleUid, 6_000, "MonkMaster" };
    }

    [Theory]
    [MemberData(nameof(ClassPoleWarPoleCases))]
    public async Task HandlerMagicAttackAgainstClassPoleWarPoleWithMatchingClassSendsSpellAnimationAndDamagesSobNpc(
        string scheduleName,
        int poleUid,
        int classCode,
        string poleName)
    {
        var sobNpc = CreateClassPoleWarPoleSobNpc(poleUid, poleName);
        var result = await RunClassPoleWarPoleMagicAttackAsync(
            sobNpc,
            CreateClassPoleWarPoleAttributes(scheduleName),
            classCode: classCode);

        AssertClassPoleWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)poleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            ClassPoleWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("WrongClass", 1_000, "WarriorGod", 0, null)]
    [InlineData("ZeroPoleHitPoints", 2_000, "Caster", 0, "Alive")]
    [InlineData("SamePlayerName", 2_000, "WarriorGod", ClassPoleWarPoleLife, "Alive")]
    [InlineData("DeadProcess", 2_000, "Caster", ClassPoleWarPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 2_000, "Caster", ClassPoleWarPoleLife, null)]
    public async Task HandlerMagicAttackAgainstClassPoleWarPoleSourceGuardBlocksAttack(
        string scenario,
        int classCode,
        string casterName,
        int poleHitPoints,
        string? process)
    {
        const string ScheduleName = "MsgWarriorGod";
        var sobNpc = CreateClassPoleWarPoleSobNpc(
            LegacySobNpcEventRules.ClassPoleWarWarriorGodPoleUid,
            "WarriorGod");
        var result = await RunClassPoleWarPoleMagicAttackAsync(
            sobNpc,
            CreateClassPoleWarPoleAttributes(ScheduleName, poleHitPoints, process),
            casterName,
            classCode);

        AssertClassPoleWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(ClassPoleWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedClassPoleWarPoleFailureDiagnostic(scenario, ScheduleName, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<ClassPoleWarPoleAttackResult> RunClassPoleWarPoleMagicAttackAsync(
        SobNpc sobNpc,
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster",
        int classCode = 2_000)
    {
        var monsterFamily = CreateMonsterFamily();
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
            mapId: LegacySobNpcEventRules.ClassPoleWarMapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: attributes,
            classCode: classCode);
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
        return new ClassPoleWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertClassPoleWarPoleManaSpent(ClassPoleWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateClassPoleWarPoleSobNpc(int poleUid, string poleName)
    {
        var poleUidText = poleUid.ToString(CultureInfo.InvariantCulture);
        return new SobNpc(
            Id: poleUid,
            Name: poleName,
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.ClassPoleWarMapId,
            X: 45,
            Y: 45,
            Life: ClassPoleWarPoleLife,
            MaxLife: ClassPoleWarPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 0,
            RawLine: poleUidText + "," + poleName + ",10,1137,10005,45,45,50000000,50000000,17,1",
            Fields:
            [
                poleUidText,
                poleName,
                "10",
                "1137",
                "10005",
                "45",
                "45",
                "50000000",
                "50000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateClassPoleWarPoleAttributes(
        string scheduleName,
        int poleHitPoints = ClassPoleWarPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetClassPoleWarPoleHitPointsAttributePairs(attributes, scheduleName, poleHitPoints);
        if (process is not null)
        {
            SetClassPoleWarAttributeTriplet(attributes, scheduleName, "Process", process);
        }

        return attributes;
    }

    private static void SetClassPoleWarPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        string scheduleName,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "HitPoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "Hitpoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "PoleHitPoints", value);
        SetClassPoleWarAttributeTriplet(attributes, scheduleName, "PoleHitpoints", value);
    }

    private static void SetClassPoleWarAttributeTriplet(
        Dictionary<string, string> attributes,
        string scheduleName,
        string suffix,
        string value)
    {
        attributes["Legacy" + scheduleName + suffix] = value;
        attributes[scheduleName + suffix] = value;
        attributes["Runtime" + scheduleName + suffix] = value;
    }

    private static string ExpectedClassPoleWarPoleFailureDiagnostic(
        string scenario,
        string scheduleName,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"{scheduleName} pole uid={sobNpc.Id} because ";
        var sourcePole = scheduleName + ".Furnitures[Pole]";
        return scenario switch
        {
            "WrongClass" => prefix +
                "wrong class: AtributesStatus.IsWarrior(client.Player.Class) is false " +
                "for classFamily=1, requiredClassFamily=2",
            "ZeroPoleHitPoints" => prefix + sourcePole + ".HitPoints is zero",
            "SamePlayerName" => prefix + "client.Player.Name matches " + sourcePole + ".Name",
            "DeadProcess" => prefix + scheduleName + ".Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + scheduleName + ".Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct ClassPoleWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
