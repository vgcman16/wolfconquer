using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ClanWarScorePoleLife = 100_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstClanWarScorePoleWithActiveClanSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunClanWarScorePoleMagicAttackAsync(CreateClanWarScorePoleAttributes());

        AssertClanWarScorePoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.ClanWarScorePoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            ClanWarScorePoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoClan", null, ClanWarScorePoleLife, "Alive")]
    [InlineData("ZeroClan", 0, ClanWarScorePoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, 0, "Alive")]
    [InlineData("DeadProcess", 501, ClanWarScorePoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, ClanWarScorePoleLife, null)]
    public async Task HandlerMagicAttackAgainstClanWarScorePoleSourceGuardBlocksAttack(
        string scenario,
        int? clanId,
        int poleHitPoints,
        string? process)
    {
        var result = await RunClanWarScorePoleMagicAttackAsync(
            CreateClanWarScorePoleAttributes(
                clanId,
                poleHitPoints,
                process));

        AssertClanWarScorePoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(ClanWarScorePoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedClanWarScorePoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<ClanWarScorePoleAttackResult> RunClanWarScorePoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateClanWarScorePoleSobNpc();
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
            mapId: LegacySobNpcEventRules.ClanWarScoreMapId,
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
        return new ClanWarScorePoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertClanWarScorePoleManaSpent(ClanWarScorePoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateClanWarScorePoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.ClanWarScorePoleUid,
            Name: "ClanWarSocre",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.ClanWarScoreMapId,
            X: 221,
            Y: 222,
            Life: ClanWarScorePoleLife,
            MaxLife: ClanWarScorePoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 95,
            RawLine: "8448,ClanWarSocre,10,1137,3839,221,222,100000000,100000000,17,1",
            Fields:
            [
                "8448",
                "ClanWarSocre",
                "10",
                "1137",
                "3839",
                "221",
                "222",
                "100000000",
                "100000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateClanWarScorePoleAttributes(
        int? clanId = 501,
        int poleHitPoints = ClanWarScorePoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (clanId is { } id)
        {
            var clanIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["ClanID"] = clanIdText;
            attributes["ClanUID"] = clanIdText;
        }

        SetClanWarScorePoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgClanWarScoreProcess"] = process;
            attributes["MsgClanWarScoreProcess"] = process;
            attributes["RuntimeMsgClanWarScoreProcess"] = process;
            attributes["LegacyClanWarScoreProcess"] = process;
            attributes["ClanWarScoreProcess"] = process;
            attributes["RuntimeClanWarScoreProcess"] = process;
        }

        return attributes;
    }

    private static void SetClanWarScorePoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgClanWarScorePoleHitPoints"] = value;
        attributes["LegacyMsgClanWarScorePoleHitpoints"] = value;
        attributes["MsgClanWarScorePoleHitPoints"] = value;
        attributes["MsgClanWarScorePoleHitpoints"] = value;
        attributes["RuntimeMsgClanWarScorePoleHitPoints"] = value;
        attributes["RuntimeMsgClanWarScorePoleHitpoints"] = value;
        attributes["LegacyClanWarScorePoleHitPoints"] = value;
        attributes["LegacyClanWarScorePoleHitpoints"] = value;
        attributes["ClanWarScorePoleHitPoints"] = value;
        attributes["ClanWarScorePoleHitpoints"] = value;
        attributes["RuntimeClanWarScorePoleHitPoints"] = value;
        attributes["RuntimeClanWarScorePoleHitpoints"] = value;
    }

    private static string ExpectedClanWarScorePoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"ClanWarScore pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.ClanWarScore.Furnitures[Pole]";
        return scenario switch
        {
            "NoClan" => prefix + "caster has no clan",
            "ZeroClan" => prefix + "caster has no clan",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "DeadProcess" => prefix + "MsgSchedules.ClanWarScore.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgSchedules.ClanWarScore.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct ClanWarScorePoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
