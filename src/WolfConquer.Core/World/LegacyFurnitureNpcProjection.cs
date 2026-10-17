using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyFurnitureNpcProjection(
    uint Id,
    int MapId,
    ushort X,
    ushort Y,
    ushort Mesh,
    ushort Type,
    string Name,
    int ItemTypeId,
    int Price)
{
    public uint Uid => Id;

    public static LegacyFurnitureNpcProjection FromFurniture(Furniture furniture)
    {
        ArgumentNullException.ThrowIfNull(furniture);

        if (!TryCreate(furniture, out var projection))
        {
            throw new ArgumentOutOfRangeException(
                nameof(furniture),
                "Furniture row contains values outside legacy NPC projection ranges.");
        }

        return projection;
    }

    public static bool TryCreate(Furniture furniture, out LegacyFurnitureNpcProjection projection)
    {
        ArgumentNullException.ThrowIfNull(furniture);

        projection = default!;
        if (!TryToUInt32(furniture.Id, out var uid) ||
            !TryToUInt16(furniture.X, out var x) ||
            !TryToUInt16(furniture.Y, out var y) ||
            !TryToUInt16(furniture.Lookface, out var mesh) ||
            !TryToUInt16(furniture.Kind, out var type))
        {
            return false;
        }

        projection = new LegacyFurnitureNpcProjection(
            uid,
            furniture.MapId,
            x,
            y,
            mesh,
            type,
            furniture.Name,
            furniture.ItemTypeId,
            furniture.Price);
        return true;
    }

    public bool MatchesRequestEntity(uint requestedUid)
    {
        return Uid == requestedUid;
    }

    public LegacyNpcSpawnPacket ToNpcSpawnPacket(ushort action = 0)
    {
        return new LegacyNpcSpawnPacket(
            Uid,
            Uid,
            Unknown: 0,
            X,
            Y,
            Mesh,
            Type,
            action);
    }

    private static bool TryToUInt32(int value, out uint result)
    {
        if (value < 0)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryToUInt16(int value, out ushort result)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
        return true;
    }
}
