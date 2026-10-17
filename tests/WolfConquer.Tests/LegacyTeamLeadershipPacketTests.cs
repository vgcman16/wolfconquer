using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamLeadershipPacketTests : IDisposable
{
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-team-leadership-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyTeamLeadershipPacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Theory]
    [InlineData(1, LegacyTeamLeadershipMode.Leader)]
    [InlineData(2, LegacyTeamLeadershipMode.Teammate)]
    public void TeamLeadershipModeDefinesLegacyNumericValues(int expected, LegacyTeamLeadershipMode mode)
    {
        Assert.Equal((uint)expected, (uint)mode);
    }

    [Fact]
    public void TeamLeadershipOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2022, WorldPacketOpcodes.LegacyTeamLeadership);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamLeadership, LegacyTeamLeadershipPacket.Opcode);
    }

    [Fact]
    public void TeamLeadershipPacketRoundTripsPackedSourceStruct()
    {
        var packet = new LegacyTeamLeadershipPacket(
            LegacyTeamLeadershipMode.Leader,
            Uid: 1_293_600,
            LeaderUid: 1_293_600,
            Count: 3,
            Unknown: 9);

        var bytes = packet.ToFrameBytes();

        Assert.Equal(
        [
            0x18, 0x00, 0xE6, 0x07,
            0x01, 0x00, 0x00, 0x00,
            0x20, 0xBD, 0x13, 0x00,
            0x20, 0xBD, 0x13, 0x00,
            0x03, 0x00, 0x00, 0x00,
            0x09, 0x00, 0x00, 0x00
        ],
        bytes);

        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void TeamLeadershipPacketPreservesUnknownModeAndSignedCount()
    {
        var packet = new LegacyTeamLeadershipPacket(
            (LegacyTeamLeadershipMode)99,
            Uid: 1,
            LeaderUid: 2,
            Count: -1,
            Unknown: uint.MaxValue);

        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));

        Assert.Equal(99U, (uint)parsed.Mode);
        Assert.Equal(-1, parsed.Count);
        Assert.Equal(uint.MaxValue, parsed.Unknown);
    }

    [Fact]
    public void TeamLeadershipPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyTeamLeadershipPacket.Opcode + 1, LegacyTeamLeadershipPacket.PayloadLength);
        writer.WriteUInt32(0, (uint)LegacyTeamLeadershipMode.Leader);

        Assert.False(LegacyTeamLeadershipPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void TeamLeadershipPacketRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyTeamLeadershipPacket.Opcode, LegacyTeamLeadershipPacket.PayloadLength - 1);

        Assert.False(LegacyTeamLeadershipPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public async Task HandlerEchoesTeamLeadershipForLoggedInSession()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync();
        var packet = new LegacyTeamLeadershipPacket(
            LegacyTeamLeadershipMode.Leader,
            Uid: 1,
            LeaderUid: 1_293_600,
            Count: 1,
            Unknown: 0);
        var countBefore = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(packet.ToFrameBytes()));

        Assert.Equal(countBefore + 1, sent.Count);
        Assert.True(LegacyTeamLeadershipPacket.TryParse(ReadFrame(sent[countBefore]), out var echoed));
        Assert.Equal(packet, echoed);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginLegacyWorldAsync()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1293600, uid: 1293600);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1293600, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WriteSnapshot(long accountId, long uid)
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
            Activeness=0/80/2/1/10/1/22/0/0/

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
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
}
