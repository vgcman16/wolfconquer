using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipmentRuntime
{
    private static ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem> PrepareRawLoad(ReadOnlySpan<byte> bytes)
    {
        var image = LegacyNativeItemLoadImage.Parse(bytes);
        var prepared = new ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem>();
        foreach (var root in image.Roots)
        {
            var record = root.Record;
            if (record.ITEM_ID == 205_901)
            {
                // This legacy filter is not new-client truth. Refuse an
                // unsupported container migration instead of losing its children.
                if (root.Deposits.Count != 0)
                    throw new InvalidDataException("Filtered raw item 205901 has deposits requiring an explicit migration policy.");
                continue; // The complete physical graph was already parsed.
            }
            var parent = ConvertRawItem(record);
            foreach (var deposit in root.Deposits)
            {
                var child = ConvertRawItem(deposit);
                if (!parent.Deposits.TryAdd(child.Uid, child))
                    throw new InvalidDataException("Raw item graph contains duplicate deposit identity.");
            }

            // The parent always retains its own UID/location. A child cannot
            // supply a route/key for a different parent value.
            if (IsRawEquipmentRoute(record) && !prepared.TryAdd(record.UID, parent))
                throw new InvalidDataException("Raw item graph contains duplicate equipment identity.");
        }
        // Perfection framing is part of Parse, not another raw generation or
        // authority to claim the full native constructor/hydration path.
        return prepared;
    }

    private static bool IsRawEquipmentRoute(LegacyNativeClientItemRecord root)
    {
        if (root.WH_ID != 0) return root.WH_ID == 100 && root.Position is > 0 and <= 59;
        if (root.Position is >= 213 and <= 215) return false;
        return root.Position is > 0 and <= 63;
    }

    private static LegacyNativeEquipmentRawItem ConvertRawItem(LegacyNativeClientItemRecord record)
    {
        // Physical parsing does not satisfy the genuine rune constructor/catalog
        // dependency for this observation. No placeholder native item is created.
        if (record.ITEM_ID / 10_000 is >= 401 and <= 403)
            throw new NotSupportedException("native rune item constructor evidence unavailable for raw observation");
        return new(record.UID, record.ITEM_ID, record.Position);
    }
}
