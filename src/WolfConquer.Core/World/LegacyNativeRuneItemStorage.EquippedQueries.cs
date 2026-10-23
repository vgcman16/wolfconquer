#nullable disable
using System;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Validated physical cache snapshot; unknown or duplicated item identity cannot grant rune effects.
    public LegacyNativeGameItemRuntime[] WornObjects
    {
        get
        {
            var client = constructorOwner;
            if (client is null) return [];
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player;
                if (player is null) return [];
                lock (player.SpellAdmissionSyncRoot)
                lock (StorageSyncRoot)
                {
                    if (!ReferenceEquals(client.Player, player) ||
                        !TryCaptureEquipped(out var context, out var snapshot)) return [];
                    var worn = SelectWorn(snapshot, context.Alternate);
                    return IsCurrent(context) ? worn : [];
                }
            }
        }
    }

    // Keep valid first-match/job rules, but invalid metadata cannot grant effects or crash the caller.
    public bool IsEquipped(string runeName, out LegacyNativeGameItemRuntime item)
    {
        item = null;
        var client = constructorOwner;
        if (client is null || runeName is null) return false;
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player;
            if (player is null) return false;
            lock (player.SpellAdmissionSyncRoot)
            lock (StorageSyncRoot)
            {
                if (!ReferenceEquals(client.Player, player) || !TryCaptureEquipped(out var context, out var snapshot) ||
                    context.Player.Reborn < 2 || context.Player.Level < 15) return false;
                var metadata = LegacyNativePoolItems.ItemsBase;
                if (metadata is null) return false;
                LegacyNativeGameItemRuntime[] wornObjects = SelectWorn(snapshot, context.Alternate);
                foreach (LegacyNativeGameItemRuntime msgGameItem in wornObjects)
                {
                    uint id = msgGameItem.ITEM_ID;
                    if (LegacyNativeItemTypeRuneMetadata.IsRune(id) &&
                        metadata.TryGetValue(id, out var row) && row is not null && row.ID == id &&
                        !string.IsNullOrEmpty(row.Name) &&
                        LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(row, context.Client) &&
                        row.Name.Contains(runeName, StringComparison.OrdinalIgnoreCase) &&
                        ReferenceEquals(LegacyNativePoolItems.ItemsBase, metadata) && IsCurrent(context))
                    {
                        item = msgGameItem;
                        return true;
                    }
                }
                return false;
            }
        }
    }

    public bool IsEquipped(string runeName)
    {
        LegacyNativeGameItemRuntime item;
        return IsEquipped(runeName, out item);
    }

    public bool IsEquipped(string runeName, ref byte itemLevel)
    {
        IsEquipped(runeName, out var item);
        if (item != null)
        {
            itemLevel = (byte)(item.ITEM_ID % 100);
        }
        else
        {
            itemLevel = 0;
        }
        return item != null;
    }
}
