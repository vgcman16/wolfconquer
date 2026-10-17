using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCountryFlagPacketTests : IDisposable
{
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-country-flag-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyCountryFlagOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2171, LegacyCountryFlagPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyCountryFlag, LegacyCountryFlagPacket.Opcode);
    }

    [Fact]
    public void LegacyCountryFlagUsesFixedSixBytePayload()
    {
        var packet = new LegacyCountryFlagPacket(Uid: 0x01020304, CountryId: 0x0506);
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(PacketHeader.Size + LegacyCountryFlagPacket.PayloadLength, bytes.Length);
        Assert.Equal(LegacyCountryFlagPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(6, LegacyCountryFlagPacket.PayloadLength);
        Assert.Equal(6, frame.Payload.Length);
        Assert.Equal(
        [
            0x0A, 0x00, 0x7B, 0x08,
            0x04, 0x03, 0x02, 0x01,
            0x06, 0x05,
        ],
        bytes);
    }

    [Fact]
    public void LegacyCountryFlagRoundTripsLegacyPayload()
    {
        var packet = new LegacyCountryFlagPacket(Uid: 1_293_600, CountryId: 16);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyCountryFlagPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyCountryFlagRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyCountryFlagPacket.Opcode + 1, LegacyCountryFlagPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyCountryFlagPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyCountryFlagRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyCountryFlagPacket.Opcode, LegacyCountryFlagPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyCountryFlagPacket.TryParse(frame, out _));
    }

    [Fact]
    public async Task HandlerUpdatesCountryFlagAndSendsItToSelfAndVisiblePeers()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600, name: "HeroOne", x: 830, y: 584);
        WriteSnapshot(accountId: 1293601, uid: 1293601, name: "Visible", x: 831, y: 584);
        WriteSnapshot(accountId: 1293602, uid: 1293602, name: "TooFar", x: 900, y: 584);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var selfSent = new List<byte[]>();
        var visibleSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        var self = CreateSendingConnection(selfSent, port: 9958);
        var visible = CreateSendingConnection(visibleSent, port: 9959);
        var far = CreateSendingConnection(farSent, port: 9960);

        await LoginAsync(handler, self, characterUid: 1293600);
        await LoginAsync(handler, visible, characterUid: 1293601);
        await LoginAsync(handler, far, characterUid: 1293602);
        var selfBeforeRequest = selfSent.Count;
        var visibleBeforeRequest = visibleSent.Count;
        var farBeforeRequest = farSent.Count;

        await handler.HandleFrameAsync(
            self,
            ReadFrame(new LegacyCountryFlagPacket(Uid: 999, CountryId: 16).ToFrameBytes()));

        Assert.Equal(selfBeforeRequest + 1, selfSent.Count);
        Assert.Equal(visibleBeforeRequest + 1, visibleSent.Count);
        Assert.Equal(farBeforeRequest, farSent.Count);
        AssertCountryFlag(selfSent[selfBeforeRequest]);
        AssertCountryFlag(visibleSent[visibleBeforeRequest]);

        static void AssertCountryFlag(byte[] bytes)
        {
            Assert.True(LegacyCountryFlagPacket.TryParse(ReadFrame(bytes), out var packet));
            Assert.Equal(1_293_600U, packet.Uid);
            Assert.Equal((ushort)16, packet.CountryId);
        }
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
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, int port)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, port), (frame, _) =>
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

    private void WriteSnapshot(long accountId, long uid, string name, long x, long y)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name={{name}}
            Class=7051
            Map=1015
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
            Identifier={{uid}}
            """);
    }
}
