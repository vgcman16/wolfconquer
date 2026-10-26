using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageStackPlan
{
    private sealed class StackBuilder
    {
        private readonly PlayerItemSnapshot source;
        private readonly LegacyItemSaveGraph graph;
        private readonly LegacyMailboxPackageItemRecipe[] grants;
        private readonly IReadOnlyDictionary<uint, LegacyMailboxPackageCatalog> metadata;
        private readonly byte[] seed;
        private readonly List<VirtualRow> rows;
        private readonly List<Change> changes = [];
        private readonly Dictionary<uint, PlayerItemSnapshotItem> physical;
        private readonly Dictionary<uint, PlayerItemSnapshotItem> attachments = [];
        private int newCount;
        private int reservedAttachmentSlots;

        internal StackBuilder(PlayerItemSnapshot source, LegacyItemSaveGraph graph,
            LegacyMailboxPackageItemRecipe[] grants, IReadOnlyDictionary<uint, LegacyMailboxPackageCatalog> metadata, byte[] seed)
        {
            this.source = source; this.graph = graph; this.grants = grants; this.metadata = metadata; this.seed = seed;
            physical = source.InventoryItems.ToDictionary(item => item.Uid);
            rows = source.InventoryItems.Select(item => new VirtualRow(RowTemplate(item), item.Uid, -1, item.StackSize, item.Color)).ToList();
        }

        internal bool TryPlan(LegacyMailboxEntry[] mails, bool claimAll, out string reason)
        {
            reason = "mailboxClaimPackageStackInvalid";
            reservedAttachmentSlots = claimAll ? mails.Count(mail => mail.ItemId != 0) : 0;
            if (source.InventoryItems.Count + reservedAttachmentSlots > 40)
            { reason = "mailboxClaimPackageCapacity"; return false; }
            var visited = new HashSet<int>();
            foreach (var mail in mails)
            {
                if (mail.Flag != 0)
                    for (var index = 0; index < grants.Length; index++)
                        if (grants[index].MailId == mail.Id &&
                            (!visited.Add(index) || !TryGrant(index, out reason))) return false;
                if (claimAll && mail.ItemId != 0 && !TryMoveAttachment(mail.ItemId)) return false;
            }
            if (visited.Count != grants.Length) return false;
            reason = string.Empty;
            return true;
        }

        private bool TryGrant(int index, out string reason)
        {
            reason = "mailboxClaimPackageStackInvalid";
            var grant = grants[index];
            if (grant.Quantity == 0 || !metadata.TryGetValue(grant.ItemId, out var item) || !item.IsCurrent() ||
                item.Digest != grant.CatalogSha256 || item.Durability != grant.Durability) return false;
            var match = item.CanStack ? FindFitting(grant, item.StackLimit) : -1;
            if (match >= 0)
            {
                var row = rows[match];
                var quantity = checked((ushort)(row.Quantity + grant.Quantity));
                rows[match] = row with { Quantity = quantity };
                changes.Add(new(index, row.ExistingUid, row.AllocationIndex, quantity, row.Color, false));
                return true;
            }
            var remaining = (uint)grant.Quantity;
            while (remaining != 0)
            {
                if (source.InventoryItems.Count + reservedAttachmentSlots + newCount >= 40)
                { reason = "mailboxClaimPackageCapacity"; return false; }
                var amount = checked((ushort)Math.Min(remaining, item.StackLimit));
                var quantity = item.CanStack ? amount : (ushort)0;
                var color = ChooseColor(seed, newCount);
                rows.Add(new(grant, 0, newCount, quantity, color));
                changes.Add(new(index, 0, newCount, quantity, color, true));
                newCount++;
                remaining -= amount;
            }
            return true;
        }

        private int FindFitting(LegacyMailboxPackageItemRecipe grant, ushort limit)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.Quantity == 0 || (uint)row.Quantity + grant.Quantity > limit || !SameTemplate(row.Template, grant)) continue;
                if (row.ExistingUid != 0 && !CompatibleOwnedRow(physical[row.ExistingUid], grant)) continue;
                return index;
            }
            return -1;
        }

        private bool TryMoveAttachment(uint uid)
        {
            if (!graph.TryGetWarehouse(1000, uid, out var owned) || physical.ContainsKey(uid)) return false;
            var moved = owned.CreateInventoryItem();
            attachments.Add(uid, moved);
            physical.Add(uid, moved);
            rows.Add(new(RowTemplate(moved), uid, -1, moved.StackSize, moved.Color));
            return true;
        }

        internal LegacyMailboxPackageStackPlan Finish() =>
            new(graph, source, grants, changes.ToArray(), rows.ToArray(), physical, attachments, newCount, seed);
    }
}
