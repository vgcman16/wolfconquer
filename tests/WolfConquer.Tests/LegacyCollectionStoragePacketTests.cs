using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyCollectionStoragePacketTests
{
    [Fact]
    public void LegacyCollectionStorageOpcodeMatchesProtocolValue()
    {
        Assert.Equal((ushort)2450, WorldPacketOpcodes.LegacyCollectionStorage);
        Assert.Equal(WorldPacketOpcodes.LegacyCollectionStorage, LegacyCollectionStoragePacket.Opcode);
    }

    [Fact]
    public void LegacyCollectionStorageFromSyntheticSnapshotWritesProtobufFields()
    {
        Assert.True(LegacyCollectionStoragePacket.TryCreateFromSnapshot(CreateSnapshotWithCollectionStorage(), out var packet));
        Assert.Equal(0U, packet.Type);
        Assert.Equal(42U, packet.CollectionId);
        Assert.Equal(new LegacyCollectionStorageActive(21, 2), packet.Active);
        Assert.Equal([new LegacyCollectionStorageActive(21, 2)], packet.Scraps);
        Assert.Equal(
        [
            new LegacyCollectionStorageEntry(101, 1, 2, 0, 3),
            new LegacyCollectionStorageEntry(102, 0, 4, 0, 5),
            new LegacyCollectionStorageEntry(103, 1, 6, 0, 7),
        ], packet.Entries);

        // Hand-authored tags and single-byte values: type, collection, scrap, then three entries.
        byte[] expectedPayload =
        [
            0x08, 0, 0x10, 42,
            0x1a, 4, 0x08, 21, 0x10, 2,
            0x22, 10, 0x08, 101, 0x10, 1, 0x18, 2, 0x20, 0, 0x28, 3,
            0x22, 10, 0x08, 102, 0x10, 0, 0x18, 4, 0x20, 0, 0x28, 5,
            0x22, 10, 0x08, 103, 0x10, 1, 0x18, 6, 0x20, 0, 0x28, 7,
        ];
        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);
        Assert.Equal(50, bytes.Length);
        Assert.Equal((ushort)50, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)2450, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)));
        Assert.Equal(expectedPayload, frame.Payload.ToArray());
    }

    [Fact]
    public void LegacyCollectionStorageRoundTrips()
    {
        Assert.True(LegacyCollectionStoragePacket.TryCreateFromSnapshot(CreateSnapshotWithCollectionStorage(), out var packet));
        Assert.True(LegacyCollectionStoragePacket.TryParse(ReadFrame(packet.ToFrameBytes()), out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.CollectionId, parsed.CollectionId);
        Assert.Equal(packet.Scraps, parsed.Scraps);
        Assert.Equal(packet.Entries, parsed.Entries);
    }

    [Fact]
    public void LegacyCollectionStorageCreatesEmptyLoadFromOldDefaultState()
    {
        var snapshot = CreateSnapshot("CollectionID=0\nCollectionStorage=0/0/\n");
        Assert.True(LegacyCollectionStoragePacket.TryCreateFromSnapshot(snapshot, out var packet));
        Assert.Equal(0U, packet.Type);
        Assert.Equal(0U, packet.CollectionId);
        Assert.Empty(packet.Scraps);
        Assert.Empty(packet.Entries);
        var frame = ReadFrame(packet.ToFrameBytes());
        Assert.Equal(4, frame.Payload.Length);
        Assert.True(LegacyCollectionStoragePacket.TryParse(frame, out var parsed));
        Assert.Empty(parsed.Scraps);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void LegacyCollectionStorageSkipsSnapshotsWithoutCollectionState()
    {
        Assert.False(LegacyCollectionStoragePacket.TryCreateFromSnapshot(CreateSnapshot(string.Empty), out _));
    }

    private static CharacterSnapshot CreateSnapshotWithCollectionStorage()
    {
        return CreateSnapshot("CollectionID=42\nCollectionStorage=1/21/2/3/101/1/2/0/3/102/0/4/0/5/103/1/6/0/7/\n");
    }

    private static CharacterSnapshot CreateSnapshot(string attributes)
    {
        return CharacterSnapshotParser.ParseText("""
            [Character]
            UID=9000001
            Body=1008
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
            """ + "\n" + attributes);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
