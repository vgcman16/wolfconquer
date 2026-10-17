using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyChatItemEntry(
    LegacyGameItemPacket Item,
    LegacyItemExtraPacket? Extra,
    LegacyItemLockPacket? Lock);

public sealed class LegacyChatItemStore
{
    private readonly ConcurrentDictionary<uint, LegacyChatItemEntry> _items = new();

    public int Count => _items.Count;

    public int CaptureLinks(
        WorldSession session,
        string message,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(message);

        var captured = 0;
        var now = (timeProvider ?? TimeProvider.System).GetLocalNow().DateTime;
        foreach (var uid in ExtractItemUids(message))
        {
            var item = session.PlayerItems.EquipmentItems.FirstOrDefault(candidate => candidate.Uid == uid) ??
                session.PlayerItems.InventoryItems.FirstOrDefault(candidate => candidate.Uid == uid);
            if (item is null)
            {
                continue;
            }

            var itemPacket = LegacyGameItemPacket.FromSnapshotItem(
                item,
                LegacyGameItemPacket.CreateRuneSpellOverride(item, itemsById),
                LegacyGameItemPacket.ChatItemMode);
            LegacyItemExtraPacket? extra = LegacyItemExtraPacket.TryCreate(item, now, out var createdExtra)
                ? createdExtra
                : null;
            LegacyItemLockPacket? itemLock = LegacyItemLockPacket.TryCreateUnlockDate(item, now, out var createdLock)
                ? createdLock
                : null;

            _items[item.Uid] = new LegacyChatItemEntry(itemPacket, extra, itemLock);
            captured++;
        }

        return captured;
    }

    public bool TryGet(uint uid, out LegacyChatItemEntry? entry)
    {
        return _items.TryGetValue(uid, out entry);
    }

    public static IReadOnlyList<uint> ExtractItemUids(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = new List<uint>();
        foreach (var segment in message.Split('[', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!segment.Contains("Item ", StringComparison.Ordinal))
            {
                continue;
            }

            var fields = segment.Split(' ');
            if (fields.Length > 2 &&
                uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
            {
                result.Add(uid);
            }
        }

        return result;
    }
}
