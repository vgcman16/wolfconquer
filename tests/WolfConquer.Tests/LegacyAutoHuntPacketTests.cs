using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAutoHuntPacketTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-auto-hunt-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyAutoHuntOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2155, WorldPacketOpcodes.LegacyAutoHunt);
        Assert.Equal(WorldPacketOpcodes.LegacyAutoHunt, LegacyAutoHuntPacket.Opcode);
    }

    [Fact]
    public void LegacyAutoHuntStartupPacketWritesFixedLegacyShape()
    {
        var bytes = LegacyAutoHuntPacket.Startup.ToFrameBytes();

        Assert.Equal(LegacyAutoHuntPacket.FrameLength, bytes.Length);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        Assert.Equal(LegacyAutoHuntPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyAutoHuntPacket.PayloadLength, frame.Payload.Length);
        Assert.True(PacketReader.TryRead(bytes, out var reader));
        Assert.Equal(LegacyAutoHuntPacket.StartupType, reader.ReadUInt16(0));
        Assert.Equal(LegacyAutoHuntPacket.StartupParameter, reader.ReadUInt64(2));
        Assert.Equal(0, reader.ReadInt32(10));
        Assert.Equal(0, reader.ReadInt32(14));
    }

    [Fact]
    public void LegacyAutoHuntPacketRoundTrips()
    {
        var packet = new LegacyAutoHuntPacket(2, 987654321);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyAutoHuntPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyAutoHuntPacketRejectsWrongReservedFields()
    {
        var writer = new PacketWriter(LegacyAutoHuntPacket.Opcode, LegacyAutoHuntPacket.PayloadLength);
        writer.WriteUInt16(0, LegacyAutoHuntPacket.StartupType);
        writer.WriteUInt64(2, LegacyAutoHuntPacket.StartupParameter);
        writer.WriteInt32(10, 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyAutoHuntPacket.TryParse(frame, out _));
    }

    [Fact]
    public async Task HandlerStartsAutoHuntWhenLoggedInCharacterIsAllowed()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeAutoHunt = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyAutoHuntPacket.Startup.ToFrameBytes()));

        Assert.Equal(
            [
                new LegacyAutoHuntPacket(0, 341),
                new LegacyAutoHuntPacket(1, 341),
            ],
            ReadAutoHuntPackets(sent.Skip(countBeforeAutoHunt)));
    }

    [Fact]
    public async Task HandlerStopsAutoHuntWhenLoggedInCharacterIsAlreadyAutoHunting()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, onAutoHunt: true);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeAutoHunt = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyAutoHuntPacket.Startup.ToFrameBytes()));

        Assert.Equal(
            [
                new LegacyAutoHuntPacket(3, 0),
            ],
            ReadAutoHuntPackets(sent.Skip(countBeforeAutoHunt)));
    }

    [Fact]
    public async Task HandlerRejectsAutoHuntOnBlockedMapWithSystemMessage()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, mapId: 10250);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LoginAsync(handler, connection, characterUid: 1293600);
        var countBeforeAutoHunt = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LegacyAutoHuntPacket.Startup.ToFrameBytes()));

        var appended = sent.Skip(countBeforeAutoHunt).ToArray();
        Assert.Contains(
            appended,
            bytes =>
                LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
                message.Content.Contains("You cannot use Auto Hunt in here."));
        Assert.DoesNotContain(new LegacyAutoHuntPacket(0, 341), ReadAutoHuntPackets(appended));
        Assert.DoesNotContain(new LegacyAutoHuntPacket(1, 341), ReadAutoHuntPackets(appended));
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

    private static IReadOnlyList<LegacyAutoHuntPacket> ReadAutoHuntPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyAutoHuntPacket>();
        foreach (var bytes in frames)
        {
            var frame = ReadFrame(bytes);
            if (LegacyAutoHuntPacket.TryParse(frame, out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private void WriteSnapshot(long accountId, long uid, long mapId = 1015, bool onAutoHunt = false)
    {
        var autoHuntLine = onAutoHunt ? "OnAutoHunt=1\r\n" : "";

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
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            {{autoHuntLine}}
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
