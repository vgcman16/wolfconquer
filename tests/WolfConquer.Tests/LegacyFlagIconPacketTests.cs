using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFlagIconPacketTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;

    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-flag-icon-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyFlagIconOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2175, WorldPacketOpcodes.LegacyFlagIcon);
        Assert.Equal(WorldPacketOpcodes.LegacyFlagIcon, LegacyFlagIconPacket.Opcode);
    }

    [Fact]
    public void LegacyFlagIconPacketWritesAndRoundTripsLegacyProtobufShape()
    {
        var packet = new LegacyFlagIconPacket(
            Type: 2,
            Uid: CharacterUid,
            Icon: LegacyFlagIcon.MagicDefender,
            Level: 5,
            Mode: 1,
            Damage: 987_654);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(
            "14007f08080210a0fa4e1808200528013086a43c",
            Convert.ToHexString(bytes).ToLowerInvariant());
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyFlagIcon, frame.Header.Opcode);
        Assert.True(LegacyFlagIconPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyFlagIconResponseFactoryForcesLegacyResponseTypeAndMode()
    {
        var packet = LegacyFlagIconPacket.CreateResponse(
            uid: CharacterUid,
            icon: LegacyFlagIcon.NobleSpirit,
            level: 7,
            damage: 55_321);

        Assert.Equal(3U, packet.Type);
        Assert.Equal(CharacterUid, packet.Uid);
        Assert.Equal(LegacyFlagIcon.NobleSpirit, packet.Icon);
        Assert.Equal(7U, packet.Level);
        Assert.Equal(30U, packet.Mode);
        Assert.Equal(55_321U, packet.Damage);

        var frame = ReadFrame(packet.ToFrameBytes());
        Assert.Equal(WorldPacketOpcodes.LegacyFlagIcon, frame.Header.Opcode);
        Assert.True(LegacyFlagIconPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public async Task HandlerLegacyFlagIconAfterLoginSendsSingleResponseWithForcedTypeAndMode()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(CharacterUid).ToFrameBytes()));
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyFlagIconPacket(
                Type: 1,
                Uid: CharacterUid,
                Icon: LegacyFlagIcon.WackeSpirit,
                Level: 12,
                Mode: 4,
                Damage: 345_678).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyFlagIconPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var response));
        Assert.Equal(3U, response.Type);
        Assert.Equal(CharacterUid, response.Uid);
        Assert.Equal(LegacyFlagIcon.WackeSpirit, response.Icon);
        Assert.Equal(12U, response.Level);
        Assert.Equal(30U, response.Mode);
        Assert.Equal(345_678U, response.Damage);
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

    private void WriteSnapshot()
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
            Identifier={{CharacterUid}}
            """);
    }
}
