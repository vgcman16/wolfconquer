using System.Buffers;
using System.Net;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyUnhandledGameplayDiagnosticsTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-unhandled-gameplay-" + Guid.NewGuid());

    [Fact]
    public async Task UnsupportedItemUsageSubtypeEmitsStructuredParityGap()
    {
        using var log = new StringWriter();
        var (handler, connection) = await CreateEnteredWorldAsync(log);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgItemUsagePacket(
                Id: 123_456,
                DwParam: 654_321,
                MsgItemUsageAction.SetEquipPosition,
                Timestamp: 42,
                DwParam2: 7,
                DwParam3: 8,
                DwParam4: 9).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=item severity=warning gap=item-use", output);
        Assert.Contains("dedupe=item:item-use:action=5:itemId=123456:opcode=2137", output);
        Assert.Contains("action=5", output);
        Assert.Contains("actionName=SetEquipPosition", output);
        Assert.Contains("itemId=123456", output);
        Assert.Contains("opcode=2137", output);
        Assert.Contains("source=MsgItemUsage", output);
        Assert.Contains("PARITY-GAP unsupported MsgItemUsage", output);
        Assert.Contains("action=SetEquipPosition actionId=5", output);
        Assert.Contains("dwParam=654321", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgItemUsage", output);
    }

    [Fact]
    public async Task UnsupportedNpcReplyEmitsStructuredClickedNpcParityGap()
    {
        using var log = new StringWriter();
        var (handler, connection) = await CreateEnteredWorldAsync(log);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 999_999)));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=clicked-npc", output);
        Assert.Contains("dedupe=npc:clicked-npc:npcId=999999:opcode=2255", output);
        Assert.Contains("npcId=999999", output);
        Assert.Contains("opcode=2255", output);
        Assert.Contains("normal NPC script/action handling is not modeled", output);
        Assert.Contains("PARITY-GAP unmodeled LegacyNpcReply", output);
        Assert.Contains("option=1", output);
        Assert.Contains("type=1", output);
        Assert.Contains("npcLookup=world-data-not-loaded", output);
        Assert.DoesNotContain("WorldServer rejected malformed LegacyNpcReply", output);
    }

    [Fact]
    public async Task UnsupportedMsgActionFallbackEmitsStructuredGameplayClickParityGap()
    {
        using var log = new StringWriter();
        var (handler, connection) = await CreateEnteredWorldAsync(log, mapId: 1002, x: 430, y: 377);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgActionPacket
            {
                ObjId = 1_293_600,
                TargetUid = 500_012,
                DwParam = 2,
                Type = (MsgActionType)999,
                MapId = 1002,
                PositionX = 431,
                PositionY = 378,
            }.ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=action severity=warning gap=unhandled-gameplay-click-action", output);
        Assert.Contains(
            "dedupe=action:unhandled-gameplay-click-action:actionSubtype=2:actionType=999:nextSeam=msgaction-runtime:opcode=2046:sourceSeam=WorldPacketHandlers.MsgAction.Fallback",
            output);
        Assert.Contains("actionSubtype=2", output);
        Assert.Contains("actionType=999", output);
        Assert.Contains("map=1002", output);
        Assert.Contains("targetUid=500012", output);
        Assert.Contains("x=431", output);
        Assert.Contains("y=378", output);
        Assert.Contains("capture client click/action for parity triage", output);
        Assert.Contains("PARITY-GAP unmodeled MsgAction", output);
        Assert.DoesNotContain("WorldServer rejected malformed MsgAction", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection)> CreateEnteredWorldAsync(
        TextWriter log,
        int mapId = 1015,
        int x = 830,
        int y = 584)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600, mapId, x, y);

        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, log);
        var connection = CreateSendingConnection();

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection);
    }

    private static ConquerTcpConnection CreateSendingConnection()
    {
        return new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (_, _) => ValueTask.CompletedTask);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType, uint npcId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, npcId);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private void WriteSnapshot(long accountId, long uid, long mapId, long x, long y)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map={{mapId}}
            X={{x}}
            Y={{y}}
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850
            DonationNobility=0

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }
}
