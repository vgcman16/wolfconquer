using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int WarOfPlayersPoleLife = 50_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstWarOfPlayersPoleWithActiveDifferentPlayerNameSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunWarOfPlayersPoleMagicAttackAsync(CreateWarOfPlayersPoleAttributes());

        AssertWarOfPlayersPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.WarOfPlayersPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            WarOfPlayersPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("ZeroPoleHitPoints", "Caster", 0, "Alive")]
    [InlineData("SamePlayerName", "WarOfPlayers", WarOfPlayersPoleLife, "Alive")]
    [InlineData("DeadProcess", "Caster", WarOfPlayersPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", "Caster", WarOfPlayersPoleLife, null)]
    public async Task HandlerMagicAttackAgainstWarOfPlayersPoleSourceGuardBlocksAttack(
        string scenario,
        string casterName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunWarOfPlayersPoleMagicAttackAsync(
            CreateWarOfPlayersPoleAttributes(
                poleHitPoints,
                process),
            casterName);

        AssertWarOfPlayersPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(WarOfPlayersPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedWarOfPlayersPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<WarOfPlayersPoleAttackResult> RunWarOfPlayersPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes,
        string casterName = "Caster")
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateWarOfPlayersPoleSobNpc();
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
            mapId: LegacySobNpcEventRules.WarOfPlayersMapId,
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
        return new WarOfPlayersPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertWarOfPlayersPoleManaSpent(WarOfPlayersPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateWarOfPlayersPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.WarOfPlayersPoleUid,
            Name: "WarOfPlayers",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.WarOfPlayersMapId,
            X: 45,
            Y: 45,
            Life: WarOfPlayersPoleLife,
            MaxLife: WarOfPlayersPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 96,
            RawLine: "6606,WarOfPlayers,10,1137,6729,45,45,50000000,50000000,17,1",
            Fields:
            [
                "6606",
                "WarOfPlayers",
                "10",
                "1137",
                "6729",
                "45",
                "45",
                "50000000",
                "50000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateWarOfPlayersPoleAttributes(
        int poleHitPoints = WarOfPlayersPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SetWarOfPlayersPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgWarOfPlayersProcess"] = process;
            attributes["MsgWarOfPlayersProcess"] = process;
            attributes["RuntimeMsgWarOfPlayersProcess"] = process;
            attributes["LegacyWarOfPlayersProcess"] = process;
            attributes["WarOfPlayersProcess"] = process;
            attributes["RuntimeWarOfPlayersProcess"] = process;
        }

        return attributes;
    }

    private static void SetWarOfPlayersPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgWarOfPlayersHitPoints"] = value;
        attributes["LegacyMsgWarOfPlayersHitpoints"] = value;
        attributes["LegacyMsgWarOfPlayersPoleHitPoints"] = value;
        attributes["LegacyMsgWarOfPlayersPoleHitpoints"] = value;
        attributes["MsgWarOfPlayersHitPoints"] = value;
        attributes["MsgWarOfPlayersHitpoints"] = value;
        attributes["MsgWarOfPlayersPoleHitPoints"] = value;
        attributes["MsgWarOfPlayersPoleHitpoints"] = value;
        attributes["RuntimeMsgWarOfPlayersHitPoints"] = value;
        attributes["RuntimeMsgWarOfPlayersHitpoints"] = value;
        attributes["RuntimeMsgWarOfPlayersPoleHitPoints"] = value;
        attributes["RuntimeMsgWarOfPlayersPoleHitpoints"] = value;
        attributes["LegacyWarOfPlayersHitPoints"] = value;
        attributes["LegacyWarOfPlayersHitpoints"] = value;
        attributes["LegacyWarOfPlayersPoleHitPoints"] = value;
        attributes["LegacyWarOfPlayersPoleHitpoints"] = value;
        attributes["WarOfPlayersHitPoints"] = value;
        attributes["WarOfPlayersHitpoints"] = value;
        attributes["WarOfPlayersPoleHitPoints"] = value;
        attributes["WarOfPlayersPoleHitpoints"] = value;
        attributes["RuntimeWarOfPlayersHitPoints"] = value;
        attributes["RuntimeWarOfPlayersHitpoints"] = value;
        attributes["RuntimeWarOfPlayersPoleHitPoints"] = value;
        attributes["RuntimeWarOfPlayersPoleHitpoints"] = value;
    }

    private static string ExpectedWarOfPlayersPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"MsgWarOfPlayers pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgWarOfPlayers.Furnitures[Pole]";
        return scenario switch
        {
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePlayerName" => prefix + "client.Player.Name matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgWarOfPlayers.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgWarOfPlayers.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct WarOfPlayersPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
