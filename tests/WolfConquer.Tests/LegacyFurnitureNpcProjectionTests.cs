using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFurnitureNpcProjectionTests
{
    [Fact]
    public void LegacyFurnitureNpcProjectionProjectsFurnitureIntoNpcLikeSpawnShape()
    {
        var furniture = Furniture(
            id: 8924,
            kind: 26,
            lookface: 837,
            mapId: 1511,
            x: 143,
            y: 130,
            name: "FlowerDressingTable",
            itemTypeId: 726083,
            price: 23_000);

        var projection = LegacyFurnitureNpcProjection.FromFurniture(furniture);

        Assert.Equal(8924U, projection.Id);
        Assert.Equal(8924U, projection.Uid);
        Assert.Equal(1511, projection.MapId);
        Assert.Equal((ushort)143, projection.X);
        Assert.Equal((ushort)130, projection.Y);
        Assert.Equal((ushort)837, projection.Mesh);
        Assert.Equal((ushort)26, projection.Type);
        Assert.Equal("FlowerDressingTable", projection.Name);
        Assert.Equal(726083, projection.ItemTypeId);
        Assert.Equal(23_000, projection.Price);
        Assert.True(projection.MatchesRequestEntity(8924));
        Assert.False(projection.MatchesRequestEntity(8925));

        Assert.Equal(
            new LegacyNpcSpawnPacket(8924, 8924, 0, 143, 130, 837, 26, 0),
            projection.ToNpcSpawnPacket());
    }

    [Fact]
    public void LegacyFurnitureNpcProjectionWritesLegacyNpcSpawnFrame()
    {
        var projection = LegacyFurnitureNpcProjection.FromFurniture(
            Furniture(id: 9001, kind: 2, lookface: 8200, mapId: 1511, x: 10, y: 20, name: "ItemBox"));

        var frame = ReadFrame(projection.ToNpcSpawnPacket(action: 9).ToFrameBytes());

        Assert.True(LegacyNpcSpawnPacket.TryParse(frame, out var parsed));
        Assert.Equal(new LegacyNpcSpawnPacket(9001, 9001, 0, 10, 20, 8200, 2, 9), parsed);
    }

    [Theory]
    [InlineData(-1, 26, 837, 143, 130)]
    [InlineData(8924, -1, 837, 143, 130)]
    [InlineData(8924, 65536, 837, 143, 130)]
    [InlineData(8924, 26, -1, 143, 130)]
    [InlineData(8924, 26, 65536, 143, 130)]
    [InlineData(8924, 26, 837, -1, 130)]
    [InlineData(8924, 26, 837, 65536, 130)]
    [InlineData(8924, 26, 837, 143, -1)]
    [InlineData(8924, 26, 837, 143, 65536)]
    public void LegacyFurnitureNpcProjectionRejectsOutOfRangePacketFields(
        int id,
        int kind,
        int lookface,
        int x,
        int y)
    {
        var furniture = Furniture(id: id, kind: kind, lookface: lookface, mapId: 1511, x: x, y: y);

        Assert.False(LegacyFurnitureNpcProjection.TryCreate(furniture, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyFurnitureNpcProjection.FromFurniture(furniture));
    }

    private static Furniture Furniture(
        int id,
        int kind,
        int lookface,
        int mapId,
        int x,
        int y,
        string name = "FlowerDressingTable",
        int itemTypeId = 726083,
        int price = 23_000)
    {
        return new Furniture(
            id,
            kind,
            lookface,
            mapId,
            x,
            y,
            name,
            itemTypeId,
            price,
            SourceFile: "furnitures.txt",
            LineNumber: 1,
            RawLine: string.Empty,
            Fields: []);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
