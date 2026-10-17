using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(99UL, 42UL, true, true, "")]
    [InlineData(42UL, 42UL, true, false, "winnerGuildId=42 matches caster GuildID")]
    [InlineData(99UL, 42UL, false, false, "NEWGUILDWAR.Proces is not Alive")]
    [InlineData(0UL, 42UL, true, false, "caster has no guild")]
    public async Task HandlerMagicAttackAgainstGuildWarPillarHonorsNewGuildWarVerifiedGate(
        ulong casterGuildId,
        ulong winnerGuildId,
        bool activeProcess,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var sobNpc = CreateNewGuildWarSobNpc(
            LegacySobNpcEventRules.GuildWarPillarUid,
            "GuildPillar",
            LegacySobNpcEventRules.PoleKind);

        await RunNewGuildWarSobGateProofAsync(
            sobNpc,
            casterGuildId,
            winnerGuildId,
            activeProcess,
            ownerGuildId: 0,
            expectedDamage,
            expectedDiagnostic);
    }

    [Theory]
    [InlineData(LegacySobNpcEventRules.GuildWarHonorPillarUid, "HonorPillar", 42U, true, false, "ownerGuildId=42 matches caster GuildID")]
    [InlineData(LegacySobNpcEventRules.GuildWarHonorPillarUid, "HonorPillar", 99U, true, true, "")]
    [InlineData(LegacySobNpcEventRules.GuildWarHonorPillarUid, "HonorPillar", 99U, false, false, "NEWGUILDWAR.Proces is not Alive")]
    public async Task HandlerMagicAttackAgainstNewGuildWarSidePillarHonorsOwnerAndProcessGate(
        int sobNpcId,
        string sobNpcName,
        uint ownerGuildId,
        bool activeProcess,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var sobNpc = CreateNewGuildWarSobNpc(
            sobNpcId,
            sobNpcName,
            LegacySobNpcEventRules.GuildWarCapturePillarKind);

        await RunNewGuildWarSobGateProofAsync(
            sobNpc,
            casterGuildId: 42,
            winnerGuildId: 0,
            activeProcess,
            ownerGuildId,
            expectedDamage,
            expectedDiagnostic);
    }

    private static async Task RunNewGuildWarSobGateProofAsync(
        SobNpc sobNpc,
        ulong casterGuildId,
        ulong winnerGuildId,
        bool activeProcess,
        uint ownerGuildId,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        if (ownerGuildId != 0)
        {
            sobNpcStates.SetOwnerGuildId(sobNpc, ownerGuildId);
        }

        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        if (activeProcess)
        {
            guildWarStates.Start();
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
            ["LegacyNewGuildWarWinnerGuildID"] = winnerGuildId.ToString(CultureInfo.InvariantCulture),
        };
        if (casterGuildId != 0)
        {
            attributes["GuildID"] = casterGuildId.ToString(CultureInfo.InvariantCulture);
        }

        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 84,
            y: 70,
            spirit: 9_500,
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates,
            GuildWarStates: guildWarStates);

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

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        var output = log.ToString();
        if (expectedDamage)
        {
            var spell = Assert.Single(ParseSpellAnimations(sent));
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(sobNpc.Life - (int)target.Damage, visibleSobNpc.Life);
            Assert.Contains("WorldServer handled legacy player-SOB magic", output);
        }
        else
        {
            Assert.Empty(ParseSpellAnimations(sent));
            Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
            Assert.Contains(expectedDiagnostic, output);
            Assert.Contains("source GUILDWAR.Verified rejected", output);
            Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
        }
    }

    private static SobNpc CreateNewGuildWarSobNpc(int id, string name, int kind)
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: kind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.GuildWarMapId,
            X: 85,
            Y: 70,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 9,
            RawLine: $"{id},{name},{kind},1137,1038,85,70,50000,50000,17,0",
            Fields:
            [
                id.ToString(CultureInfo.InvariantCulture),
                name,
                kind.ToString(CultureInfo.InvariantCulture),
                "1137",
                "1038",
                "85",
                "70",
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }
}
