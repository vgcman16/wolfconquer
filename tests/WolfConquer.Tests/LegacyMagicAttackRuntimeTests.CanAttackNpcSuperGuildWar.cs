using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int SuperGuildWarPoleLife = 800_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstSuperGuildWarPoleWithActiveEnemyGuildSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunSuperGuildWarPoleMagicAttackAsync(CreateSuperGuildWarPoleAttributes());

        AssertSuperGuildWarPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.SuperGuildWarPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            SuperGuildWarPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", null, SuperGuildWarPoleLife, 777, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, 0, 777, "Alive")]
    [InlineData("SameWinnerGuild", 501, SuperGuildWarPoleLife, 501, "Alive")]
    [InlineData("DeadProcess", 501, SuperGuildWarPoleLife, 777, "Dead")]
    [InlineData("IdleProcess", 501, SuperGuildWarPoleLife, 777, "Idle")]
    [InlineData("MissingProcessDefaultsDead", 501, SuperGuildWarPoleLife, 777, null)]
    public async Task HandlerMagicAttackAgainstSuperGuildWarPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        int poleHitPoints,
        int winnerGuildId,
        string? process)
    {
        var result = await RunSuperGuildWarPoleMagicAttackAsync(
            CreateSuperGuildWarPoleAttributes(
                guildId,
                poleHitPoints,
                winnerGuildId,
                process));

        AssertSuperGuildWarPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(SuperGuildWarPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedSuperGuildWarPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    [Fact]
    public void SuperGuildWarEntryNpcIsNotAttackPole()
    {
        Assert.False(LegacySobNpcEventRules.IsSuperGuildWarPole(CreateSuperGuildWarEntryNpc()));
    }

    private static async Task<SuperGuildWarPoleAttackResult> RunSuperGuildWarPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateSuperGuildWarPoleSobNpc();
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
            mapId: LegacySobNpcEventRules.SuperGuildWarMapId,
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
        return new SuperGuildWarPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertSuperGuildWarPoleManaSpent(SuperGuildWarPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateSuperGuildWarPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.SuperGuildWarPoleUid,
            Name: "Poll",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.SuperGuildWarMapId,
            X: 222,
            Y: 131,
            Life: SuperGuildWarPoleLife,
            MaxLife: SuperGuildWarPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 124,
            RawLine: "820,Poll,10,1137,3868,222,131,800000000,800000000,17,1",
            Fields:
            [
                "820",
                "Poll",
                "10",
                "1137",
                "3868",
                "222",
                "131",
                "800000000",
                "800000000",
                "17",
                "1",
            ]);
    }

    private static SobNpc CreateSuperGuildWarEntryNpc()
    {
        return new SobNpc(
            Id: 25_015,
            Name: "SuperGuildWar",
            Kind: 2,
            Lookface: 64_970,
            MapId: 1_002,
            X: 342,
            Y: 241,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 66,
            RawLine: "25015,SuperGuildWar,2,64970,1002,342,241,0,0,1,1",
            Fields:
            [
                "25015",
                "SuperGuildWar",
                "2",
                "64970",
                "1002",
                "342",
                "241",
                "0",
                "0",
                "1",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateSuperGuildWarPoleAttributes(
        int? guildId = 501,
        int poleHitPoints = SuperGuildWarPoleLife,
        int winnerGuildId = 777,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guildId is { } id)
        {
            var guildIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["GuildID"] = guildIdText;
            attributes["GuildId"] = guildIdText;
        }

        SetSuperGuildWarAttributeTriplets(attributes, poleHitPoints, winnerGuildId);
        if (process is not null)
        {
            SetSuperGuildWarAttributeTriplet(attributes, "Process", process);
            SetSuperGuildWarAttributeTriplet(attributes, "Proces", process);
        }

        return attributes;
    }

    private static void SetSuperGuildWarAttributeTriplets(
        Dictionary<string, string> attributes,
        int poleHitPoints,
        int winnerGuildId)
    {
        var poleHitPointsText = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        SetSuperGuildWarAttributeTriplet(attributes, "HitPoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "Hitpoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "PoleHitPoints", poleHitPointsText);
        SetSuperGuildWarAttributeTriplet(attributes, "PoleHitpoints", poleHitPointsText);

        var winnerGuildIdText = winnerGuildId.ToString(CultureInfo.InvariantCulture);
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildID", winnerGuildIdText);
        SetSuperGuildWarAttributeTriplet(attributes, "WinnerGuildId", winnerGuildIdText);
    }

    private static void SetSuperGuildWarAttributeTriplet(
        Dictionary<string, string> attributes,
        string suffix,
        string value)
    {
        string[] aliases = ["MsgSuperGuildWar", "SuperGuildWar", "MsgSchedulesSuperGuildWar"];
        foreach (var alias in aliases)
        {
            attributes["Legacy" + alias + suffix] = value;
            attributes[alias + suffix] = value;
            attributes["Runtime" + alias + suffix] = value;
        }
    }

    private static string ExpectedSuperGuildWarPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgSchedules.SuperGuildWar pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.SuperGuildWar.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "client.Player.MyGuild is null",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SameWinnerGuild" => prefix + "client.Player.GuildID matches MsgSchedules.SuperGuildWar.Winner.GuildID=501",
            "DeadProcess" => prefix + "MsgSchedules.SuperGuildWar.Proces is Dead",
            "IdleProcess" => prefix + "MsgSchedules.SuperGuildWar.Proces is Idle",
            "MissingProcessDefaultsDead" => prefix + "MsgSchedules.SuperGuildWar.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct SuperGuildWarPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
