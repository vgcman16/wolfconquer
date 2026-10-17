using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBroadcastPacketTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-broadcast-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyBroadcastOpcodesMatchLegacyFinalizeValues()
    {
        Assert.Equal((ushort)2052, WorldPacketOpcodes.LegacyBroadcast);
        Assert.Equal((ushort)2277, WorldPacketOpcodes.LegacyBroadcastList);
        Assert.Equal(WorldPacketOpcodes.LegacyBroadcast, LegacyBroadcastPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyBroadcastList, LegacyBroadcastListPacket.Opcode);
    }

    [Fact]
    public void LegacyBroadcastPacketRoundTripsStringListShape()
    {
        var packet = new LegacyBroadcastPacket(
            LegacyBroadcastAction.BroadcastMessage,
            Parameter: 7,
            ["hello world"]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyBroadcast, frame.Header.Opcode);
        Assert.True(LegacyBroadcastPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Action, parsed.Action);
        Assert.Equal(packet.Parameter, parsed.Parameter);
        Assert.Equal(packet.Strings, parsed.Strings);
    }

    [Fact]
    public void LegacyBroadcastListPacketRoundTripsFixedWidthEntryShape()
    {
        var packet = new LegacyBroadcastListPacket(
            Page: 2,
            Segment: 1,
            Count: 1,
            [
                new LegacyBroadcastListEntry(
                    Id: 15,
                    Index: 3,
                    EntityId: 1_293_600,
                    EntityName: "HeroOne",
                    SpentConquerPoints: 100_000,
                    Message: "Server broadcast",
                    UnionRank: 4),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyBroadcastList, frame.Header.Opcode);
        Assert.Equal(164, frame.Payload.Length);
        Assert.True(LegacyBroadcastListPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Page, parsed.Page);
        Assert.Equal(packet.Segment, parsed.Segment);
        var entry = Assert.Single(parsed.Entries);
        Assert.Equal(15U, entry.Id);
        Assert.Equal(3U, entry.Index);
        Assert.Equal(1_293_600U, entry.EntityId);
        Assert.Equal("HeroOne", entry.EntityName);
        Assert.Equal(100_000U, entry.SpentConquerPoints);
        Assert.Equal("Server broadcast", entry.Message);
        Assert.Equal(4U, entry.UnionRank);
    }

    [Fact]
    public async Task HandlerCreatesBroadcastDebitsConquerPointsAndServesMyMessagesList()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, conquerPoints: 5);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeBroadcast = sent.Count;
        var longMessage = new string('A', 90);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBroadcastPacket(
                LegacyBroadcastAction.BroadcastMessage,
                Parameter: 0,
                [longMessage]).ToFrameBytes()));

        Assert.Equal(countBeforeBroadcast + 2, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[countBeforeBroadcast]), out var message));
        Assert.Equal(LegacyMsgMessagePacket.BroadcastChatType, message.ChatType);
        Assert.Equal("HeroOne", message.Content[0]);
        Assert.Equal(new string('A', 80), message.Content[3]);
        Assert.True(LegacyBroadcastPacket.TryParse(ReadFrame(sent[countBeforeBroadcast + 1]), out var ack));
        Assert.Equal(LegacyBroadcastAction.BroadcastMessage, ack.Action);
        Assert.Equal(1U, ack.Parameter);
        Assert.Equal([new string('A', 80)], ack.Strings);

        var countBeforeInsufficientCp = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBroadcastPacket(
                LegacyBroadcastAction.BroadcastMessage,
                Parameter: 0,
                ["second"]).ToFrameBytes()));

        Assert.Equal(countBeforeInsufficientCp, sent.Count);

        var countBeforeList = sent.Count;
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBroadcastPacket(
                LegacyBroadcastAction.MyMessages,
                Parameter: 0,
                []).ToFrameBytes()));

        Assert.Equal(countBeforeList + 1, sent.Count);
        Assert.True(LegacyBroadcastListPacket.TryParse(ReadFrame(sent[countBeforeList]), out var list));
        var entry = Assert.Single(list.Entries);
        Assert.Equal(1_293_600U, entry.EntityId);
        Assert.Equal("HeroOne", entry.EntityName);
        Assert.Equal(new string('A', 80), entry.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static ValueTask LoginAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        uint characterUid)
    {
        return handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(characterUid).ToFrameBytes()));
    }

    private void WriteSnapshot(long accountId, long uid, long conquerPoints)
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
            Map=1015
            X=830
            Y=584
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
            ConquerPoints={{conquerPoints}}
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
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

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }
}
