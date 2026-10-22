using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        // Compose AFTER declaration-only cachedToxin foundation6D9375, NOT its historical local-sum runtime.
        private bool RebuildNativePerfectionCache()
        {
            if (!Refresh()) return false;
            byte Rune(string name) => Context.WorldData is { } data &&
                TryGetLegacyEligibleEquippedRuneLevel(Session, data, name, out var level) ? level : (byte)0;
            if (!LegacyNativePerfectionClient.Rebuild(Context.Sessions, Context.Connection,
                    Request.Scope.OwnerUid, Rune, Request.Scope.OwnerNativeClientIdentity)) return false;
            // Retain the old one-field observational projection, never use it as this cache's authority.
            var toxin = LegacyNativePerfectionClient.Read(Context.Sessions, Context.Connection,
                Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity).ToxinEraser;
            return Commit(session => session with
            {
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(session) with
                    { PerfectionToxinEraser = toxin },
            });
        }

        private bool CaptureNativePerfectionRelicLucky(PlayerItemSnapshotItem item)
        {
            if (!Refresh()) return false;
            if (item.Position % 50 != 13 || Context.WorldData is not { } data ||
                item.ItemId > int.MaxValue ||
                !data.ItemsById.TryGetValue((int)item.ItemId, out var itemRows) || itemRows.Count == 0 ||
                item.AnimaItemId != 0 &&
                    (item.AnimaItemId > int.MaxValue ||
                     !data.ItemsById.TryGetValue((int)item.AnimaItemId, out var animaRows) || animaRows.Count == 0))
                return true; // Native DB/Anima indexer failure exits this item's try before its Relic loop.
            foreach (var encoded in new[] { item.Attribute1, item.Attribute2, item.Attribute3, item.Attribute4, item.Attribute5 })
                if (encoded != 0 && encoded % 100 == 14)
                    if (!LegacyNativePerfectionClient.AddLucky(Context.Sessions, Context.Connection,
                            Request.Scope.OwnerUid, unchecked((ushort)(encoded / 1_000U)),
                            Request.Scope.OwnerNativeClientIdentity)) return false;
            // RelicAttribute.Value is ushort. Final CreatePrestigePoints later RESETS this old cache value.
            return true;
        }
    }
}
