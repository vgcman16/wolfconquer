using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ClanWarPoleUid = 890;
    private const int ClanWarPoleMapId = 1_002;
    private const ulong ClanWarCasterClanId = 42;
    private const ulong ClanWarEnemyWinnerClanId = 99;

    [Fact]
    public async Task HandlerThunderAgainstClanWarPoleAllowsDamageWhenCasterIsInActiveWarAndWinnerIsOtherClan()
    {
        await RunClanWarPoleGateProofAsync(
            CreateClanWarAttributes(
                casterClanId: ClanWarCasterClanId,
                currentWarActive: true,
                inWar: true,
                winnerClanId: ClanWarEnemyWinnerClanId),
            expectedDamage: true,
            expectedDiagnostic: "");
    }

    [Fact]
    public async Task HandlerThunderAgainstClanWarPoleBlocksWhenCasterHasNoClan()
    {
        await RunClanWarPoleGateProofAsync(
            CreateClanWarAttributes(
                casterClanId: null,
                currentWarActive: true,
                inWar: true,
                winnerClanId: ClanWarEnemyWinnerClanId),
            expectedDamage: false,
            expectedDiagnostic: "ClanWar pole uid=890 because caster has no clan");
    }

    [Fact]
    public async Task HandlerThunderAgainstClanWarPoleBlocksWhenThereIsNoCurrentClanWar()
    {
        await RunClanWarPoleGateProofAsync(
            CreateClanWarAttributes(
                casterClanId: ClanWarCasterClanId,
                currentWarActive: false,
                inWar: true,
                winnerClanId: ClanWarEnemyWinnerClanId),
            expectedDamage: false,
            expectedDiagnostic: "ClanWar pole uid=890 because MsgSchedules.ClanWar.CurentWar is unavailable");
    }

    [Fact]
    public async Task HandlerThunderAgainstClanWarPoleBlocksWhenCasterIsNotInCurrentClanWar()
    {
        await RunClanWarPoleGateProofAsync(
            CreateClanWarAttributes(
                casterClanId: ClanWarCasterClanId,
                currentWarActive: true,
                inWar: false,
                winnerClanId: ClanWarEnemyWinnerClanId),
            expectedDamage: false,
            expectedDiagnostic: "ClanWar pole uid=890 because curentWar.InWar(client) is false");
    }

    [Fact]
    public async Task HandlerThunderAgainstClanWarPoleBlocksWhenWinnerClanMatchesCasterClan()
    {
        await RunClanWarPoleGateProofAsync(
            CreateClanWarAttributes(
                casterClanId: ClanWarCasterClanId,
                currentWarActive: true,
                inWar: true,
                winnerClanId: ClanWarCasterClanId),
            expectedDamage: false,
            expectedDiagnostic: "ClanWar pole uid=890 because winnerClanId=42 matches caster ClanUID");
    }

    private static async Task RunClanWarPoleGateProofAsync(
        IReadOnlyDictionary<string, string> attributes,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateClanWarPoleSobNpc();
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
            mapId: sobNpc.MapId,
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
        Assert.Equal(493, updatedSession.Character.MinMana);

        var spells = ParseSpellAnimations(sent);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        var output = log.ToString();
        if (expectedDamage)
        {
            var spell = Assert.Single(spells);
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(sobNpc.X, spell.X);
            Assert.Equal(sobNpc.Y, spell.Y);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)ClanWarPoleUid), target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(sobNpc.Life - (int)target.Damage, visibleSobNpc.Life);
            Assert.Contains("WorldServer handled legacy player-SOB magic", output);
            Assert.DoesNotContain("source CanAttackNpc.Verified rejected", output);
            return;
        }

        Assert.Empty(spells);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Contains("source CanAttackNpc.Verified rejected", output);
        Assert.Contains(expectedDiagnostic, output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    private static Dictionary<string, string> CreateClanWarAttributes(
        ulong? casterClanId,
        bool currentWarActive,
        bool inWar,
        ulong winnerClanId)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyClanWarActive"] = currentWarActive.ToString(CultureInfo.InvariantCulture),
            ["ClanWarActive"] = currentWarActive.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarInWar"] = inWar.ToString(CultureInfo.InvariantCulture),
            ["ClanWarInWar"] = inWar.ToString(CultureInfo.InvariantCulture),
            ["LegacyClanWarWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture),
            ["ClanWarWinnerClanID"] = winnerClanId.ToString(CultureInfo.InvariantCulture),
        };

        if (casterClanId is { } clanId)
        {
            attributes["ClanID"] = clanId.ToString(CultureInfo.InvariantCulture);
            attributes["ClanUID"] = clanId.ToString(CultureInfo.InvariantCulture);
        }

        return attributes;
    }

    private static SobNpc CreateClanWarPoleSobNpc()
    {
        return new SobNpc(
            Id: ClanWarPoleUid,
            Name: "ClanWarPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: ClanWarPoleMapId,
            X: 85,
            Y: 70,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 11,
            RawLine: "890,ClanWarPole,10,1137,1002,85,70,50000,50000,17,0",
            Fields:
            [
                "890",
                "ClanWarPole",
                "10",
                "1137",
                "1002",
                "85",
                "70",
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }
}
