using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTitlePacketTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;

    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-title-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyTitleOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2152, WorldPacketOpcodes.LegacyTitle);
        Assert.Equal(WorldPacketOpcodes.LegacyTitle, LegacyTitlePacket.Opcode);
    }

    [Fact]
    public void LegacyTitleActionsMatchLegacyValues()
    {
        Assert.Equal((byte)1, (byte)LegacyTitleAction.Enqueue);
        Assert.Equal((byte)2, (byte)LegacyTitleAction.Dequeue);
        Assert.Equal((byte)3, (byte)LegacyTitleAction.Change);
        Assert.Equal((byte)4, (byte)LegacyTitleAction.List);
    }

    [Fact]
    public void LegacyTitleUsesFixedSixBytePayload()
    {
        var packet = new LegacyTitlePacket(0x01020304, 5, LegacyTitleAction.List);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(PacketHeader.Size + LegacyTitlePacket.PayloadLength, bytes.Length);
        Assert.Equal(LegacyTitlePacket.Opcode, frame.Header.Opcode);
        Assert.Equal(6, LegacyTitlePacket.PayloadLength);
        Assert.Equal(6, frame.Payload.Length);
        Assert.Equal(
        [
            0x0A, 0x00, 0x68, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x05, 0x04,
        ],
        bytes);
    }

    [Fact]
    public void LegacyTitleRoundTripsLegacyPayload()
    {
        var packet = new LegacyTitlePacket(CharacterUid, 7, LegacyTitleAction.Change);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyTitlePacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyTitleAcceptsTrailingLiveClientBytes()
    {
        var writer = new PacketWriter(LegacyTitlePacket.Opcode, LegacyTitlePacket.PayloadLength + 2);
        writer.WriteUInt32(0, CharacterUid);
        writer.Payload[4] = 7;
        writer.Payload[5] = (byte)LegacyTitleAction.List;
        writer.Payload[6] = 0xAA;
        writer.Payload[7] = 0xBB;

        Assert.True(LegacyTitlePacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(CharacterUid, parsed.Uid);
        Assert.Equal(7, parsed.Title);
        Assert.Equal(LegacyTitleAction.List, parsed.Action);
    }

    [Fact]
    public void LegacyTitleRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyTitlePacket.Opcode + 1, LegacyTitlePacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyTitlePacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyTitleRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyTitlePacket.Opcode, LegacyTitlePacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyTitlePacket.TryParse(frame, out _));
    }

    [Fact]
    public async Task HandlerLegacyTitleListSendsOwnedTitlesAsEnqueueFrames()
    {
        var (handler, connection, sent) = await LoginWithTitlesAsync("2/5/5/0/7/", myTitle: 2);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTitlePacket(999, 0, LegacyTitleAction.List).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 3, sent.Count);
        AssertTitle(sent[countBeforeRequest], 2, LegacyTitleAction.Enqueue);
        AssertTitle(sent[countBeforeRequest + 1], 5, LegacyTitleAction.Enqueue);
        AssertTitle(sent[countBeforeRequest + 2], 7, LegacyTitleAction.Enqueue);
    }

    [Fact]
    public async Task HandlerLegacyTitleChangeSendsOwnedTitleChange()
    {
        var (handler, connection, sent) = await LoginWithTitlesAsync("2,5,7", myTitle: 2);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTitlePacket(999, 7, LegacyTitleAction.Change).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertTitle(sent[countBeforeRequest], 7, LegacyTitleAction.Change);
    }

    [Fact]
    public async Task HandlerLegacyTitleChangeAcceptsZeroTitle()
    {
        var (handler, connection, sent) = await LoginWithTitlesAsync("2,5,7", myTitle: 2);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTitlePacket(999, 0, LegacyTitleAction.Change).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        AssertTitle(sent[countBeforeRequest], 0, LegacyTitleAction.Change);
    }

    [Fact]
    public async Task HandlerLegacyTitleChangeRejectsUnownedTitle()
    {
        var (handler, connection, sent) = await LoginWithTitlesAsync("2,5,7", myTitle: 2);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyTitlePacket(999, 8, LegacyTitleAction.Change).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)> LoginWithTitlesAsync(
        string titles,
        byte myTitle)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(titles, myTitle);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9961), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(CharacterUid).ToFrameBytes()));
        return (handler, connection, sent);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static void AssertTitle(byte[] bytes, byte title, LegacyTitleAction action)
    {
        Assert.True(LegacyTitlePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        Assert.Equal(title, packet.Title);
        Assert.Equal(action, packet.Action);
    }

    private void WriteSnapshot(string titles, byte myTitle)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, CharacterUid + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
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
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            RacePoints=0
            ChampionPoints=0
            GuildDontion=0
            PkPoints=0
            Online=0
            Day={{DateTime.Now.DayOfYear}}
            MyTitle={{myTitle}}
            Titles={{titles}}

            [PaidNobility]
            Rank=King
            Identifier={{CharacterUid}}
            """);
    }
}
