using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCaptureEquipmentMoveCatalog(WorldPacketHandlerContext context, uint itemId,
        out EquipmentMoveCatalog captured, out ItemType itemType)
    {
        captured = null!; itemType = null!;
        if (context.WorldData is not { } data || itemId > int.MaxValue ||
            !data.ItemsById.TryGetValue((int)itemId, out var rows) || rows.Count != 1 || rows[0] is not { } source ||
            source.Id != itemId) return false;
        var fields = FullAccountKnownRows(source.Fields, 4096);
        int[] required = [ItemType.RequiredClassFieldIndex, ItemType.RequiredLevelFieldIndex,
            ItemType.RequiredGenderFieldIndex, ItemType.RequiredStrengthFieldIndex, ItemType.RequiredAgilityFieldIndex];
        foreach (var index in required)
            if (index >= fields.Length || !uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
        captured = new(source, fields); itemType = new(source.Id, source.Name, fields); return true;
    }

    private static bool EquipmentMoveCatalogIsCurrent(WorldPacketHandlerContext context, EquipmentMoveCatalog? expected)
    {
        if (expected is null) return true; // Unequip consumes no catalog admission fact.
        return TryCaptureEquipmentMoveCatalog(context, checked((uint)expected.Source.Id), out var current, out _) &&
            ReferenceEquals(current.Source, expected.Source) && current.Fields.SequenceEqual(expected.Fields);
    }
}
