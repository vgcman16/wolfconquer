using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using WolfConquer.Core.Data;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOwnedNativeItemImage
{
    // Callback-bearing interface reads and callback-free backing reads are kept
    // separate. Unknown collection implementations cannot be commit witnesses.
    private sealed class PlainList<T> where T : class
    {
        private readonly IReadOnlyList<T> source;
        internal IReadOnlyList<T> Source => source;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal readonly T[] Values;
        internal PlainList(IReadOnlyList<T> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
            object backing = TrustedBacking(source);
            array = backing as T[]; list = backing as List<T>;
            if (array is null && list is null) throw Invalid("collection has no trusted plain backing");
            int count = source.Count;
            if (count < 0 || count > CharacterSnapshotFileBaseline.MaximumBytes / 184)
                throw Invalid("collection count exceeds item image bounds");
            Values = new T[count];
            for (int index = 0; index < count; index++) Values[index] = source[index] ?? throw Invalid("missing collection row");
            if (!IsCurrentPlain()) throw Invalid("collection changed during capture");
        }
        private static object TrustedBacking(object value)
        {
            if (value is T[] or List<T>) return value;
            Type type = value.GetType();
            bool generated = type.Assembly == typeof(LegacyOwnedNativeItemImage).Assembly && type.Namespace is null &&
                type.IsSealed && type.IsGenericType && type.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
                type.GetGenericTypeDefinition().Name is "<>z__ReadOnlyArray`1" or "<>z__ReadOnlyList`1" or "<>z__ReadOnlySingleElementList`1";
            bool trusted = type == typeof(ReadOnlyCollection<T>) || generated;
            if (!trusted) return value;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly) continue;
                object? candidate = field.GetValue(value);
                if (candidate is T[] or List<T>) return candidate;
                // A genuine compiler singleton has an immutable reference cell;
                // copying that readonly cell is a plain witness, not a wrapper callback.
                if (generated && field.FieldType == typeof(T) && candidate is T one) return new T[] { one };
            }
            return value;
        }
        internal void RequireCurrent()
        {
            if (source.Count != Values.Length) throw Invalid("collection count changed");
            for (int index = 0; index < Values.Length; index++)
                if (!ReferenceEquals(source[index], Values[index])) throw Invalid("collection row changed");
        }
        internal bool IsCurrentPlain()
        {
            int count = array is not null ? array.Length : list!.Count;
            if (count != Values.Length) return false;
            for (int index = 0; index < count; index++)
                if (!ReferenceEquals(array is not null ? array[index] : list![index], Values[index])) return false;
            return true;
        }
    }

    internal sealed class SavedRow
    {
        internal readonly PlayerItemSnapshotItem Source, Value;
        internal readonly LegacyNativeClientItemRecord Record;
        internal readonly SavedRow[] Children;
        private readonly PlainList<PlayerItemSnapshotItem> children;
        internal SavedRow(PlayerItemSnapshotItem source, HashSet<uint> seen, bool child)
        {
            Source = source; ValueSourceChildren = source.DepositedItems;
            if (source.Uid == 0 || source.ItemId == 0 || !seen.Add(source.Uid)) throw Invalid("noncanonical item identity");
            children = new(source.DepositedItems);
            if (child && children.Values.Length != 0) throw Invalid("nested deposits are not representable");
            Children = new SavedRow[children.Values.Length];
            var copies = new PlayerItemSnapshotItem[Children.Length];
            for (int index = 0; index < Children.Length; index++)
            { Children[index] = new(children.Values[index], seen, true); copies[index] = Children[index].Value; }
            Value = source.WithDepositedItems(copies);
            Record = RecordOf(Value, copies.Length);
        }
        internal void RequireSourceCurrent()
        {
            children.RequireCurrent();
            foreach (var child in Children) child.RequireSourceCurrent();
        }
        internal bool SourceIsCurrentPlain()
        {
            if (!ReferenceEquals(Source.DepositedItems, childrenSource) || !children.IsCurrentPlain() || !SameSavedFields(Source, Value, Children.Length)) return false;
            foreach (var child in Children) if (!child.SourceIsCurrentPlain()) return false;
            return true;
        }
        private IReadOnlyList<PlayerItemSnapshotItem> childrenSource => ValueSourceChildren;
        private readonly IReadOnlyList<PlayerItemSnapshotItem> ValueSourceChildren;
        internal bool SameValue(SavedRow other)
        {
            if (Children.Length != other.Children.Length || !SameSavedFields(Value, other.Value, Children.Length)) return false;
            for (int index = 0; index < Children.Length; index++) if (!Children[index].SameValue(other.Children[index])) return false;
            return true;
        }
    }

    internal sealed class SnapshotProof
    {
        internal readonly PlayerItemSnapshot Source, Canonical;
        internal readonly SavedRow[][] Rows;
        private readonly PlainList<PlayerItemSnapshotItem>[] groups;
        internal SnapshotProof(PlayerItemSnapshot source)
        {
            ArgumentNullException.ThrowIfNull(source); Source = source;
            var stores = Stores(source); SourceGroups = stores; groups = new PlainList<PlayerItemSnapshotItem>[7]; Rows = new SavedRow[7][];
            var copies = new PlayerItemSnapshotItem[7][]; var seen = new HashSet<uint>();
            for (int group = 0; group < groups.Length; group++)
            {
                groups[group] = new(stores[group]); Rows[group] = new SavedRow[groups[group].Values.Length];
                copies[group] = new PlayerItemSnapshotItem[Rows[group].Length];
                for (int index = 0; index < Rows[group].Length; index++)
                {
                    var row = new SavedRow(groups[group].Values[index], seen, false);
                    if (!ValidPlacement(group, row.Value)) throw Invalid("item location conflicts with canonical container");
                    Rows[group][index] = row; copies[group][index] = row.Value;
                }
            }
            Canonical = new(copies[0], copies[1], copies[2], copies[3], copies[4], copies[5]) { WarehouseItems = copies[6] };
            if (!LegacyItemSaveGraph.TryCapture(Canonical, out _)) throw Invalid("canonical item image cannot be persisted exactly");
            RequireCurrent();
            if (!IsCurrentPlain()) throw Invalid("snapshot changed during capture");
        }
        internal void RequireCurrent()
        {
            foreach (var group in groups) group.RequireCurrent();
            foreach (var rows in Rows) foreach (var row in rows) row.RequireSourceCurrent();
        }
        internal bool IsCurrentPlain()
        {
            if (!ReferenceEquals(Source.InventoryItems, Group(0)) || !ReferenceEquals(Source.RelicFuseItems, Group(1)) ||
                !ReferenceEquals(Source.MythsoulBagItems, Group(2)) || !ReferenceEquals(Source.EquipmentItems, Group(3)) ||
                !ReferenceEquals(Source.RuneItems, Group(4)) || !ReferenceEquals(Source.WardrobeItems, Group(5)) ||
                !ReferenceEquals(Source.WarehouseItems, Group(6))) return false;
            foreach (var group in groups) if (!group.IsCurrentPlain()) return false;
            foreach (var rows in Rows) foreach (var row in rows) if (!row.SourceIsCurrentPlain()) return false;
            return true;
        }
        private IReadOnlyList<PlayerItemSnapshotItem> Group(int index) => SourceGroups[index];
        private readonly IReadOnlyList<PlayerItemSnapshotItem>[] SourceGroups;
        internal bool SameValue(SnapshotProof other)
        {
            for (int group = 0; group < Rows.Length; group++)
            {
                if (Rows[group].Length != other.Rows[group].Length) return false;
                for (int index = 0; index < Rows[group].Length; index++) if (!Rows[group][index].SameValue(other.Rows[group][index])) return false;
            }
            return true;
        }
    }

    private static IReadOnlyList<PlayerItemSnapshotItem>[] Stores(PlayerItemSnapshot x) =>
        [x.InventoryItems, x.RelicFuseItems, x.MythsoulBagItems, x.EquipmentItems, x.RuneItems, x.WardrobeItems, x.WarehouseItems];
    private static bool ValidPlacement(int group, PlayerItemSnapshotItem x) => group switch
    {
        0 => x.WarehouseId == 0 && x.Position == 0,
        1 => x.WarehouseId == 0 && x.Position is >= 213 and <= 215,
        2 => x.WarehouseId == 0 && x.Position == 217,
        3 => x.WarehouseId == 0 && x.Position is > 0 and <= 63 || x.WarehouseId == 100 && x.Position is > 0 and <= 59,
        4 => x.WarehouseId == 0 && (x.Position is 211 or 212 or >= 101 and <= 135),
        5 => x.WarehouseId == 100 && x.Position == 0,
        6 => x.WarehouseId is not (0 or 100),
        _ => false
    };
    private static bool SameValue<T>(T left, T right) where T : unmanaged =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref left, 1)).SequenceEqual(
            MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref right, 1)));
    private static bool SameSavedFields(PlayerItemSnapshotItem a, PlayerItemSnapshotItem b, int children) =>
        SameValue(RecordOf(a, children), RecordOf(b, children)) && a.PerfectionLevel == b.PerfectionLevel &&
        a.PerfectionProgress == b.PerfectionProgress && a.OwnerUid == b.OwnerUid &&
        string.Equals(a.OwnerName, b.OwnerName, StringComparison.Ordinal) && string.Equals(a.Signature, b.Signature, StringComparison.Ordinal);
    private static LegacyNativeClientItemRecord RecordOf(PlayerItemSnapshotItem x, int children) => new()
    {
        UID = x.Uid,
        ITEM_ID = x.ItemId,
        Durability = x.Durability,
        MaximDurability = x.MaximumDurability,
        Position = x.Position,
        SocketProgress = x.SocketProgress,
        SocketOne = (Flags.Gem)x.SocketOne,
        SocketTwo = (Flags.Gem)x.SocketTwo,
        Effect = (Flags.ItemEffect)x.Effect,
        Plus = x.Plus,
        Bless = x.Bless,
        Bound = x.Bound,
        Enchant = x.Enchant,
        Suspicious = x.Suspicious,
        Locked = x.Locked,
        PlusProgress = x.PlusProgress,
        Inscribed = x.Inscribed,
        Activate = x.Activate,
        TimeLeftInMinutes = x.TimeLeftInMinutes,
        StackSize = x.StackSize,
        WH_ID = x.WarehouseId,
        Color = (Flags.Color)x.Color,
        UnLockTimer = x.UnlockTimer,
        ItemPoints = x.ItemPoints,
        PurificationItemID = x.PurificationItemId,
        PurificationLevel = x.PurificationLevel,
        PurificationDuration = x.PurificationDuration,
        PurificationAddedOn = x.PurificationAddedOn,
        EffectID = x.RefineryEffectId,
        EffectLevel = x.RefineryEffectLevel,
        EffectPercent = x.RefineryEffectPercent,
        EffectPercent2 = x.RefineryEffectPercent2,
        EffectDuration = x.RefineryEffectDuration,
        EffectAddedOn = x.RefineryEffectAddedOn,
        AnimaItemID = x.AnimaItemId,
        RuneEXP = x.RuneExperience,
        Attribute1 = x.Attribute1,
        Attribute2 = x.Attribute2,
        Attribute3 = x.Attribute3,
        Attribute4 = x.Attribute4,
        Attribute5 = x.Attribute5,
        Expiration = x.Expiration,
        MythSoul = x.MythSoul,
        MythSoulPoints = x.MythSoulPoints,
        DepositeCount = checked((uint)children)
    };
}
