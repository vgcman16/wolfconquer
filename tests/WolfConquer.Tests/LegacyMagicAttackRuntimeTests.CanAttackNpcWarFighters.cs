using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int WarFightersPoleLife = 50_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstWarFightersPoleWithActiveDifferentPlayerNameSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunWarFightersPoleMagicAttackAsync(CreateWarFightersPoleAttributes());

        AssertWarFightersPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.WarFightersPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            WarFightersPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("ZeroPoleHitPoints", "Caster", 0, "Alive")]
    [InlineData("SamePlayerName", "WarFighters", WarFightersPoleLife, "Alive")]
    [InlineData("DeadProcess", "Caster", WarFightersPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", "Caster", WarFightersPoleLife, null)]
    public async Task HandlerMagicAttackAgainstWarFightersPoleSourceGuardBlocksAttack(
        string scenario,
        string casterName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunWarFightersPoleMagicAttackAsync(
            CreateWarFightersPoleAttributes(
                poleHitPoints,
                process),
            casterName);

        AssertWarFightersPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(WarFightersPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedWarFightersPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    [Fact]
    public void WarFightersEventNpcIsNotAttackPole()
    {
        Assert.False(LegacySobNpcEventRules.IsWarFightersPole(CreateWarFightersEventNpc()));
    }

    private static async Task<WarFightersPoleAttackResult> RunWarFightersPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster")
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateWarFightersPoleSobNpc();
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
            mapId: LegacySobNpcEventRules.WarFightersMapId,
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
        return new WarFightersPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertWarFightersPoleManaSpent(WarFightersPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateWarFightersPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.WarFightersPoleUid,
            Name: "WarFighters",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.WarFightersMapId,
            X: 166,
            Y: 124,
            Life: WarFightersPoleLife,
            MaxLife: WarFightersPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 109,
            RawLine: "9406,WarFighters,10,1137,11073,166,124,50000000,50000000,17,1",
            Fields:
            [
                "9406",
                "WarFighters",
                "10",
                "1137",
                "11073",
                "166",
                "124",
                "50000000",
                "50000000",
                "17",
                "1",
            ]);
    }

    private static SobNpc CreateWarFightersEventNpc()
    {
        return new SobNpc(
            Id: 22_213,
            Name: "WarFighters",
            Kind: 2,
            Lookface: 50_977,
            MapId: 1_002,
            X: 327,
            Y: 346,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 110,
            RawLine: "22213,WarFighters,2,50977,1002,327,346,0,0,1,1",
            Fields:
            [
                "22213",
                "WarFighters",
                "2",
                "50977",
                "1002",
                "327",
                "346",
                "0",
                "0",
                "1",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateWarFightersPoleAttributes(
        int poleHitPoints = WarFightersPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetWarFightersPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgWarFightersProcess"] = process;
            attributes["MsgWarFightersProcess"] = process;
            attributes["RuntimeMsgWarFightersProcess"] = process;
            attributes["LegacyWarFightersProcess"] = process;
            attributes["WarFightersProcess"] = process;
            attributes["RuntimeWarFightersProcess"] = process;
        }

        return attributes;
    }

    private static void SetWarFightersPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgWarFightersHitPoints"] = value;
        attributes["LegacyMsgWarFightersHitpoints"] = value;
        attributes["LegacyMsgWarFightersPoleHitPoints"] = value;
        attributes["LegacyMsgWarFightersPoleHitpoints"] = value;
        attributes["MsgWarFightersHitPoints"] = value;
        attributes["MsgWarFightersHitpoints"] = value;
        attributes["MsgWarFightersPoleHitPoints"] = value;
        attributes["MsgWarFightersPoleHitpoints"] = value;
        attributes["RuntimeMsgWarFightersHitPoints"] = value;
        attributes["RuntimeMsgWarFightersHitpoints"] = value;
        attributes["RuntimeMsgWarFightersPoleHitPoints"] = value;
        attributes["RuntimeMsgWarFightersPoleHitpoints"] = value;
        attributes["LegacyWarFightersHitPoints"] = value;
        attributes["LegacyWarFightersHitpoints"] = value;
        attributes["LegacyWarFightersPoleHitPoints"] = value;
        attributes["LegacyWarFightersPoleHitpoints"] = value;
        attributes["WarFightersHitPoints"] = value;
        attributes["WarFightersHitpoints"] = value;
        attributes["WarFightersPoleHitPoints"] = value;
        attributes["WarFightersPoleHitpoints"] = value;
        attributes["RuntimeWarFightersHitPoints"] = value;
        attributes["RuntimeWarFightersHitpoints"] = value;
        attributes["RuntimeWarFightersPoleHitPoints"] = value;
        attributes["RuntimeWarFightersPoleHitpoints"] = value;
    }

    private static string ExpectedWarFightersPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgWarFighters pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgWarFighters.Furnitures[Pole]";
        return scenario switch
        {
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePlayerName" => prefix + "client.Player.Name matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgWarFighters.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgWarFighters.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct WarFightersPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
