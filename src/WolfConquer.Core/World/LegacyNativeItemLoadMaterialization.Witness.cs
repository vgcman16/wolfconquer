using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeItemLoadMaterialization
{
    [System.Flags]
    private enum MetadataUse { Constructor = 1, Job = 2 }

    private sealed class MetadataRow
    {
        internal readonly uint Id, Spell, Class;
        internal readonly MetadataUse Use;
        internal readonly LegacyNativeItemRuneMetadata Source;
        internal MetadataRow(uint id, MetadataUse use, LegacyNativeItemTypeRuneMetadata owner)
        {
            if (!owner.TryGetValue(id, out var row) || row is null || row.ID != id)
                throw Invalid("missing or mismatched actual rune metadata row");
            Id = id; Use = use; Source = row; Spell = row.RuneSpellID; Class = row.Class;
        }
        internal bool HasCurrentLookup(LegacyNativeItemTypeRuneMetadata owner) =>
            owner.TryGetValue(Id, out var row) && ReferenceEquals(row, Source);
        internal bool HasCurrentValues() => Source.ID == Id &&
            ((Use & MetadataUse.Constructor) == 0 || Source.RuneSpellID == Spell) &&
            ((Use & MetadataUse.Job) == 0 || Source.Class == Class);
    }

    internal sealed class MaterializationWitness
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly uint uid, playerClass;
        private readonly LegacyNativeItemTypeRuneMetadata? metadata;
        private readonly Dictionary<uint, MetadataRow> rows = new();
        private readonly List<(LegacyNativeGameItemRuntime Item, uint Id, uint Spell, Flags.ItemEffect Effect)> constructors = [];
        internal uint GuildId { get; }
        internal ushort GearSlot { get; }
        internal LegacyNativeWardrobeRuntime? Wardrobe { get; }

        internal MaterializationWitness(LegacyNativeItemLoadImage image, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, Destination required)
        {
            ArgumentNullException.ThrowIfNull(client); ArgumentNullException.ThrowIfNull(player);
            this.client = client; this.player = player; uid = player.UID;
            playerClass = player.Class; GuildId = player.GuildID; GearSlot = LegacyNativeGameItemRuntime.MythsoulBag;
            if ((required & (Destination.WardrobeGarment | Destination.WardrobeMount)) != 0)
            {
                Wardrobe = client.MyWardrobe ?? throw Invalid("missing actual wardrobe");
                if (!Wardrobe.IsOwnedBy(client)) throw Invalid("foreign wardrobe owner");
            }
            var uses = new Dictionary<uint, MetadataUse>();
            foreach (var root in image.Roots)
            {
                var record = root.Record;
                if (record.ITEM_ID == 205_901) continue;
                ObserveUse(uses, record.ITEM_ID, ClassifyRoot(record, GearSlot));
                foreach (var child in root.Deposits) ObserveUse(uses, child.ITEM_ID, Destination.None);
            }
            if (uses.Count != 0)
            {
                metadata = LegacyNativePoolItems.ItemsBase ?? throw Invalid("required real rune metadata owner is missing");
                foreach (var use in uses) rows.Add(use.Key, new MetadataRow(use.Key, use.Value, metadata));
            }
            RequireCurrent();
        }

        private static void ObserveUse(Dictionary<uint, MetadataUse> uses, uint id, Destination route)
        {
            MetadataUse needed = 0;
            if (LegacyNativeItemTypeRuneMetadata.IsRune(id)) needed |= MetadataUse.Constructor;
            if ((route & Destination.Rune) != 0) needed |= MetadataUse.Job;
            if (needed != 0) uses[id] = uses.GetValueOrDefault(id) | needed;
        }

        internal void ObserveConstructor(LegacyNativeGameItemRuntime item)
        {
            RequireCurrent();
            if (!LegacyNativeItemTypeRuneMetadata.IsRune(item.ITEM_ID)) return;
            var row = rows[item.ITEM_ID];
            uint spell = row.Spell == item.ITEM_ID ? row.Spell - row.Spell % 100 + 1 : row.Spell;
            var effect = row.Spell == item.ITEM_ID ? Flags.ItemEffect.RuneEffect : Flags.ItemEffect.None;
            if (item.SpellID != spell || item.Effect != effect) throw Invalid("rune constructor used changed metadata");
            constructors.Add((item, item.ITEM_ID, spell, effect));
        }

        internal void PrepareRunePosition(LegacyNativeGameItemRuntime item)
        {
            RequireCurrent();
            if (!LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(rows[item.ITEM_ID].Source, client))
                item.Position = 212; // Correct the genuine detached item, not a reused file row.
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            // Real catalog lookups may invoke its application comparer. Finish
            // every such callback before checking any final scalar/binding state.
            if (metadata is not null)
            {
                if (!ReferenceEquals(LegacyNativePoolItems.ItemsBase, metadata)) throw Invalid("rune metadata owner changed");
                foreach (var row in rows.Values)
                    if (!row.HasCurrentLookup(metadata)) throw Invalid("queried rune metadata mapping changed");
            }
            RequirePureState();
        }

        private void RequirePureState()
        {
            if (uid == 0 || !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
                player.UID != uid || player.Class != playerClass || player.GuildID != GuildId ||
                LegacyNativeGameItemRuntime.MythsoulBag != GearSlot)
                throw Invalid("player, guild, class or bag-slot evidence changed");
            if (Wardrobe is not null && (!ReferenceEquals(client.MyWardrobe, Wardrobe) || !Wardrobe.IsOwnedBy(client)))
                throw Invalid("wardrobe evidence changed");
            if (metadata is not null)
            {
                if (!ReferenceEquals(LegacyNativePoolItems.ItemsBase, metadata)) throw Invalid("rune metadata owner changed");
                int found = 0;
                // Enumerating the actual catalog performs no key-comparer calls.
                // This private uint-key table has only the framework comparer;
                // it cannot run the public catalog's application callbacks.
                foreach (var entry in metadata)
                    if (rows.TryGetValue(entry.Key, out var expected))
                    {
                        if (!ReferenceEquals(entry.Value, expected.Source)) throw Invalid("earlier metadata mapping changed");
                        found++;
                    }
                if (found != rows.Count) throw Invalid("queried metadata key disappeared");
                foreach (var row in rows.Values)
                    if (!row.HasCurrentValues()) throw Invalid("queried rune metadata values changed");
            }
            foreach (var row in constructors)
                if (row.Item.ITEM_ID != row.Id || row.Item.SpellID != row.Spell || row.Item.Effect != row.Effect)
                    throw Invalid("prepared rune constructor result changed");
        }
    }
}
