using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOverheadLeagueInfoPacketTests
{
    [Fact]
    public void LegacyOverheadLeagueInfoOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2197, WorldPacketOpcodes.LegacyOverheadLeagueInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyOverheadLeagueInfo, LegacyOverheadLeagueInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyOverheadLeagueInfoStartupRequiresUnion()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
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
            PkPoints=0
            Online=0
            """);

        Assert.False(LegacyOverheadLeagueInfoPacket.TryCreateStartup(snapshot, out _));
    }

    [Fact]
    public void LegacyOverheadLeagueInfoStartupUsesCharacterUidAndServerId()
    {
        var snapshot = CharacterSnapshotParser.ParseText(
            """
            [Character]
            UID=1293600
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
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
            PkPoints=0
            Online=0
            UnionUID=1370
            """);

        Assert.True(LegacyOverheadLeagueInfoPacket.TryCreateStartup(snapshot, out var packet));

        Assert.Equal(0U, packet.Type);
        Assert.Equal(1_293_600U, packet.Uid);
        Assert.Equal(0U, packet.DwParam1);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(2U, packet.Id);
        Assert.Equal(string.Empty, packet.Text);
    }

    [Fact]
    public void LegacyOverheadLeagueInfoPacketWritesLegacyProtobufShape()
    {
        var packet = new LegacyOverheadLeagueInfoPacket(
            Type: 0,
            Uid: 1_293_600,
            DwParam1: 0,
            DwParam2: 0,
            DwParam3: 0,
            Id: 2,
            Text: string.Empty);

        Assert.Equal(
            "12009508080010a0fa4e1800200028003002",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyOverheadLeagueInfoPacketRoundTrips()
    {
        var packet = new LegacyOverheadLeagueInfoPacket(
            Type: 1,
            Uid: 1_293_600,
            DwParam1: 2,
            DwParam2: 3,
            DwParam3: 4,
            Id: 5,
            Text: "league");
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyOverheadLeagueInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
