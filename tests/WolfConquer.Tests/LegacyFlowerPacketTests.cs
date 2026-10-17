using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFlowerPacketTests
{
    [Fact]
    public void LegacyFlowerLoginFreeFlowersWritesSyntheticStartupLayout()
    {
        var snapshot = Snapshot(flowers: "7/0/0/0/0/0/0/0/0/", body: 1008);

        Assert.True(LegacyFlowerPacket.TryCreateLoginFreeFlowers(snapshot, out var packet));

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(60, bytes.Length);
        Assert.Equal(WorldPacketOpcodes.LegacyFlower, frame.Header.Opcode);
        Assert.Equal((ushort)60, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)));
        Assert.All(bytes.AsSpan(8, 8).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(7U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, 4)));
        Assert.All(bytes.AsSpan(20).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.True(LegacyFlowerPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyFlowerPacket.FlowerSenderAction, parsed.Action);
        Assert.Equal(7U, parsed.RedRoses);
        Assert.Equal(0U, parsed.FlowerType);
        Assert.Equal(0U, parsed.Effect);
    }

    [Fact]
    public void LegacyFlowerSkipsWhenNoFreeFlowersArePersisted()
    {
        var snapshot = Snapshot(flowers: "0/0/0/0/0/0/0/0/0/", body: 1008);

        Assert.False(LegacyFlowerPacket.TryCreateLoginFreeFlowers(snapshot, out _));
    }

    private static CharacterSnapshot Snapshot(string flowers, long body)
    {
        return CharacterSnapshotParser.ParseText($$"""
            [Character]
            UID=9000001
            Body={{body}}
            Face=1
            Name=FixtureHero
            Class=10
            Map=1002
            X=20
            Y=30
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=40
            Haire=1
            Experience=0
            PMap=1002
            PMapX=20
            PMapY=30
            Agility=10
            Strength=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=100
            MinMana=50
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=1000
            PkPoints=0
            Online=0
            Flowers={{flowers}}
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
