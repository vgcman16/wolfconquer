using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHouseRuntime
{
    private sealed record SavedFurniture(uint Key, LegacyNativeNpcRuntime Source, DBNpc Value);

    private sealed record HouseSaveCapture(LegacyNativeGameClientRuntime Client,
        LegacyNativePlayerRuntime Player, LegacyNativeHouseRuntime House,
        ConcurrentDictionary<uint, LegacyNativeHouseRuntime> Poll,
        ConcurrentDictionary<uint, LegacyNativeNpcRuntime> Furniture,
        byte Level, string Path, SavedFurniture[] Rows, byte[] Image)
    {
        internal bool IsCurrent()
        {
            if (!SamePlayer(Client, Player, House.OwnerUid) || !ReferenceEquals(Client.MyHouse, House) ||
                !ReferenceEquals(HousePoll, Poll) || !Poll.TryGetValue(House.OwnerUid, out var current) ||
                !ReferenceEquals(current, House) || !ReferenceEquals(House.Furnitures, Furniture) ||
                Level is < 1 or > 6 || House.Level != Level || Furniture.Count != Rows.Length ||
                !HousePathComparer.Equals(House.SourcePath, Path) ||
                !HousePathComparer.Equals(ConfiguredHousePath(House.OwnerUid), Path)) return false;
            foreach (var row in Rows)
                if (!Furniture.TryGetValue(row.Key, out var currentRow) ||
                    !ReferenceEquals(currentRow, row.Source) || !DBNpc.Create(currentRow).Equals(row.Value)) return false;
            return true;
        }
    }

    private static HouseSaveCapture CaptureSave(LegacyNativeGameClientRuntime client)
    {
        if (client.Player is not { } player || player.UID == 0 || !ReferenceEquals(player.Owner, client) ||
            client.MyHouse is not { } house || !ValidOwner(house, player.UID) ||
            house.SourcePath is not { } path || !HousePathComparer.Equals(ConfiguredHousePath(house.OwnerUid), path) ||
            HousePoll is not { } poll || !poll.TryGetValue(house.OwnerUid, out var current) ||
            !ReferenceEquals(current, house))
            throw new InvalidOperationException("houseSaveOwnerInvalid");

        var furniture = house.Furnitures;
        var count = furniture.Count;
        var maximumCount = (MaximumHouseFileBytes - HouseHeaderSize) / HouseFurnitureSize;
        if (count > maximumCount) throw new InvalidOperationException("houseSaveFurnitureCountInvalid");
        var rows = new List<SavedFurniture>(count);
        foreach (var (key, row) in furniture)
        {
            // Bound the walk even if an uncooperating raw writer grows the public map.
            if (rows.Count == maximumCount || key == 0 || row is null || row.UID != key)
                throw new InvalidOperationException("houseSaveFurnitureInvalid");
            var value = DBNpc.Create(row);
            if (value.UID != key) throw new InvalidOperationException("houseSaveFurnitureChanged");
            rows.Add(new(key, row, value));
        }
        var capturedRows = rows.ToArray();
        var level = house.Level;
        if (level is < 1 or > 6) throw new InvalidOperationException("houseSaveLevelInvalid");
        var image = EncodeHouse(level, capturedRows);
        var capture = new HouseSaveCapture(client, player, house, poll, furniture, level, path, capturedRows, image);
        if (!capture.IsCurrent()) throw new InvalidOperationException("houseSaveOwnerOrGraphChanged");
        return capture;
    }

    private static byte[] EncodeHouse(byte level, SavedFurniture[] rows)
    {
        var bytes = new byte[checked(HouseHeaderSize + rows.Length * HouseFurnitureSize)];
        bytes[0] = level;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), rows.Length);
        for (var index = 0; index < rows.Length; index++)
            WriteFurniture(bytes.AsSpan(HouseHeaderSize + index * HouseFurnitureSize, HouseFurnitureSize), rows[index].Value);
        return bytes;
    }

    private static void WriteFurniture(Span<byte> target, DBNpc row)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(target, row.UID);
        BinaryPrimitives.WriteUInt32LittleEndian(target[4..], row.UnKnow);
        BinaryPrimitives.WriteUInt16LittleEndian(target[8..], row.X);
        BinaryPrimitives.WriteUInt16LittleEndian(target[10..], row.Y);
        BinaryPrimitives.WriteUInt16LittleEndian(target[12..], row.Mesh);
        BinaryPrimitives.WriteUInt16LittleEndian(target[14..], (ushort)row.NpcType);
        target[16] = (byte)row.ObjType;
        // Offset 17 is native struct alignment, not a modeled persistent field.
        target[17] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(target[18..], row.Sort);
        BinaryPrimitives.WriteUInt32LittleEndian(target[20..], row.DynamicID);
        BinaryPrimitives.WriteUInt32LittleEndian(target[24..], row.Map);
    }
}
