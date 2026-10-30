using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using Destination = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Destination;
using Placement = WolfConquer.Core.World.LegacyNativeItemLoadOwners.Placement;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedNativeItemImage
{
    internal sealed class Node
    {
        internal LegacyNativeGameItemRuntime Item { get; }
        internal IReadOnlyList<Node> Children { get; }
        internal LegacyNativeClientItemRecord Record => saved.Record;
        private readonly Node[] children;
        private readonly SavedRow saved;
        private readonly CatalogEvidence construction;
        private readonly NativeState state;
        private Node(SavedRow saved, CatalogEvidence construction, Node[] children, LegacyNativeGameItemRuntime item)
        {
            this.saved = saved; this.construction = construction; this.children = children; Item = item;
            Children = Array.AsReadOnly(children); state = new(item);
        }
        internal bool IsCurrentPlain()
        {
            if (!state.IsCurrentPlain(Item)) return false;
            foreach (var child in children) if (!child.IsCurrentPlain()) return false;
            return true;
        }
        internal static Node Create(SavedRow saved, CatalogEvidence construction, Node? old)
        {
            bool sameBody = old is not null && saved.SameValue(old.saved) &&
                construction.SameConstructor(saved.Record.ITEM_ID, old.construction) && old.IsCurrentPlain();
            if (sameBody)
            {
                bool sameChildren = true;
                foreach (var child in old!.children)
                    if (!construction.SameConstructor(child.Record.ITEM_ID, child.construction)) sameChildren = false;
                if (sameChildren) return old;
            }
            var children = new Node[saved.Children.Length];
            for (int index = 0; index < children.Length; index++)
            {
                Node? prior = null;
                if (old is not null) foreach (var child in old.children)
                    if (child.Record.UID == saved.Children[index].Record.UID) { prior = child; break; }
                children[index] = Create(saved.Children[index], construction, prior);
            }
            var record = saved.Record;
            // The genuine native constructor must not silently narrow saved words.
            uint[] words = [record.Attribute1, record.Attribute2, record.Attribute3, record.Attribute4, record.Attribute5];
            foreach (uint word in words)
                if ((uint)new LegacyNativeRelicAttribute(word) != word) throw Invalid("unrepresentable native relic word");
            var item = record.GetDataItem(construction);
            item.PerfectionLevel = saved.Value.PerfectionLevel; item.PerfectionProgress = saved.Value.PerfectionProgress;
            item.OwnerUID = saved.Value.OwnerUid; item.OwnerName = saved.Value.OwnerName; item.Signature = saved.Value.Signature;
            foreach (var child in children)
                if (!item.Deposite.TryAdd(child.Item.Uid, child.Item)) throw Invalid("duplicate native child");
            return new(saved, construction, children, item);
        }
    }

    private sealed class NativeState
    {
        private readonly LegacyNativeClientItemRecord record;
        private readonly DateTime end;
        private readonly LegacyNativeItemExtra.Purification purification;
        private readonly LegacyNativeItemExtra.Refinery refinery;
        private readonly LegacyNativeRelicAttribute[] attributes, values;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> deposits;
        private readonly KeyValuePair<uint, LegacyNativeGameItemRuntime>[] entries;
        private readonly uint spell, owner, level, progress, green;
        private readonly ushort rank, length, packetId, unknown;
        private readonly LegacyNativeGameItemFlags.ItemMode mode;
        private readonly string name, signature;
        internal NativeState(LegacyNativeGameItemRuntime x)
        {
            record = NativeRecord(x); end = x.EndDate; purification = x.Purification; refinery = x.Refinary;
            attributes = x.RelicAttributes; values = (LegacyNativeRelicAttribute[])attributes.Clone();
            deposits = x.Deposite;
            if (!ReferenceEquals(deposits.Comparer, EqualityComparer<uint>.Default)) throw Invalid("native deposits have a callback comparer");
            entries = deposits.ToArray();
            spell = x.SpellID; owner = x.OwnerUID; level = x.PerfectionLevel; progress = x.PerfectionProgress;
            name = x.OwnerName; signature = x.Signature; mode = x.Mode; green = x.ProgresGreen;
            rank = x.PerfectionRank; length = x.Leng; packetId = x.PacketID; unknown = x.UnKnow;
        }
        internal bool IsCurrentPlain(LegacyNativeGameItemRuntime x)
        {
            if (!SameValue(record, NativeRecord(x)) || !SameValue(end, x.EndDate) || !SameValue(purification, x.Purification) ||
                !SameValue(refinery, x.Refinary) || !ReferenceEquals(attributes, x.RelicAttributes) || attributes.Length != values.Length ||
                !ReferenceEquals(deposits, x.Deposite) || x.SpellID != spell || x.OwnerUID != owner || x.PerfectionLevel != level ||
                x.PerfectionProgress != progress || !string.Equals(x.OwnerName, name, StringComparison.Ordinal) ||
                !string.Equals(x.Signature, signature, StringComparison.Ordinal) || x.Mode != mode || x.ProgresGreen != green ||
                x.PerfectionRank != rank || x.Leng != length || x.PacketID != packetId || x.UnKnow != unknown) return false;
            for (int index = 0; index < values.Length; index++) if (!SameValue(attributes[index], values[index])) return false;
            // Genuine default uint ConcurrentDictionary lookup/count have no
            // application comparer callbacks and allocate no enumerator. There
            // is no shared cursor for concurrent unlocked observers to race.
            if (!ReferenceEquals(deposits.Comparer, EqualityComparer<uint>.Default) || deposits.Count != entries.Length) return false;
            foreach (var entry in entries)
                if (!deposits.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry.Value)) return false;
            return deposits.Count == entries.Length;
        }
    }

    private static LegacyNativeClientItemRecord NativeRecord(LegacyNativeGameItemRuntime x) => new()
    {
        UID = x.Uid,
        ITEM_ID = x.ITEM_ID,
        Durability = x.Durability,
        MaximDurability = x.MaximDurability,
        Position = x.Position,
        SocketProgress = x.SocketProgress,
        SocketOne = x.SocketOne,
        SocketTwo = x.SocketTwo,
        Effect = x.Effect,
        Plus = x.Plus,
        Bless = x.Bless,
        Bound = x.Bound,
        Enchant = x.Enchant,
        Suspicious = x.Suspicious,
        Locked = x.Locked,
        PlusProgress = x.PlusProgress,
        Inscribed = x.Inscribed,
        Activate = x.Activate,
        StackSize = x.StackSize,
        WH_ID = x.WH_ID,
        Color = x.Color,
        UnLockTimer = x.UnLockTimer,
        ItemPoints = x.ItemPoints,
        AnimaItemID = x.AnimaItemID,
        RuneEXP = x.RuneEXP,
        MythSoul = x.MythSoul,
        MythSoulPoints = x.MythSoulPoints,
        TimeLeftInMinutes = x.OwnedTimeLeftStorage
    };

    private static void Route(int group, Node node, List<Placement> placements)
    {
        var item = node.Item;
        Destination destination = group switch
        {
            0 => Destination.Inventory, 1 => Destination.Relic, 2 => Destination.Gear,
            3 => Destination.Equipment, 4 => Destination.Rune,
            5 => Wardrobe(item.ITEM_ID), 6 => Destination.Warehouse,
            _ => throw Invalid("unknown canonical group")
        };
        if (group == 3 && item.WH_ID == 100) placements.Add(new(item, Wardrobe(item.ITEM_ID)));
        placements.Add(new(item, destination, group == 6 ? item.WH_ID : 0));
    }
    private static Destination Wardrobe(uint id) => LegacyNativeItemPosition.ItemPosition(id) switch
    { 9 => Destination.WardrobeGarment, 17 => Destination.WardrobeMount,
        _ => throw Invalid("canonical WH100 root has no native wardrobe bucket") };
}
