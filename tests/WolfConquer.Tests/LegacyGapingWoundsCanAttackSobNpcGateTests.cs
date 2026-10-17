using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerGapingWoundsCanAttackSobNpcUnionWarPoleNoUnionSendsDialogAndRejectsDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateSourceUnionWarPoleSobNpc();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId],
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: CreateUnionWarAttributes(
                casterUnionId: null,
                casterGuildId: UnionWarCasterGuildId,
                poleHitPoints: sobNpc.Life,
                winnerUnionId: UnionWarEnemyWinnerUnionId,
                process: "Alive"));
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
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                checked((uint)sobNpc.Id),
                checked((ushort)sobNpc.X),
                checked((ushort)sobNpc.Y))).ToFrameBytes()));

        Assert.Contains(
            ParseLegacyMessages(sent),
            message => message.ChatType == LegacyMsgMessagePacket.DialogChatType &&
                message.Content.Contains("Sorry you not have Union."));
        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(
            spell,
            checked((uint)sobNpc.Id),
            checked((ushort)sobNpc.X),
            checked((ushort)sobNpc.Y));
        Assert.Empty(spell.Targets);
        Assert.Empty(ParseCruelShadeSpellPackets(sent));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        var output = log.ToString();
        Assert.Contains("source CanAttackNpc.Verified rejected", output);
        Assert.Contains("UnionWar pole uid=22348 because caster has no union", output);
        Assert.Contains("targets=0", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    private static SobNpc CreateSourceUnionWarPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.UnionWarPoleUid,
            Name: "UnionWarPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.UnionWarMapId,
            X: 85,
            Y: 70,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 12,
            RawLine: "22348,UnionWarPole,10,1137,22348,85,70,50000,50000,17,0",
            Fields:
            [
                "22348",
                "UnionWarPole",
                "10",
                "1137",
                "22348",
                "85",
                "70",
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }
}
