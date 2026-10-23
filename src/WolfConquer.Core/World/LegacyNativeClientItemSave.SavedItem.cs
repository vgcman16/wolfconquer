using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    private sealed class SavedItem
    {
        internal LegacyNativeGameItemRuntime Item { get; }
        internal uint Uid { get; }
        internal MapImage Deposits { get; }
        private LegacyNativeClientItemRecord wire;
        private LegacyNativeClientItemRecord normalized;
        private LegacyNativeRelicAttribute[] attributes = null!;
        private DateTime endDate;
        private uint activate;
        private uint level;
        private uint progress;
        private uint ownerUid;
        private string? name;
        private string? signature;
        private bool prepared;

        internal SavedItem(LegacyNativeGameItemRuntime item)
        {
            Item = item; Uid = item.Uid;
            Deposits = MapImage.Capture(item.Deposite ?? throw InvalidGraph("nullDeposits"));
        }

        internal void Prepare(ref LegacyNativeClientItemRecord reused)
        {
            if (Item.Uid != Uid || Item.RelicAttributes is null || Item.RelicAttributes.Length != 5 ||
                !ReferenceEquals(Item.Deposite, Deposits.Map) || !Deposits.IsCurrent()) throw InvalidGraph("invalidSavedRow");
            attributes = Item.RelicAttributes;
            endDate = Item.EndDate; activate = Item.Activate;
            _ = reused.GetDBItem(Item); // Actual whole native conversion, same reused DB record.
            reused.DepositeCount = checked((uint)Deposits.Entries.Length);
            wire = reused;
            normalized = reused;
            normalized.RemainingTime = 0; // Wall-clock observation is not a mutable input field.
            level = Item.PerfectionLevel; progress = Item.PerfectionProgress; ownerUid = Item.OwnerUID;
            name = Item.OwnerName; signature = Item.Signature;
            prepared = true;
        }

        internal bool HasPerfection => level != 0 || progress != 0;
        internal bool IsEquipment => LegacyNativeItemPosition.ItemPosition(wire.ITEM_ID) != 0;
        internal bool HasPerfectionOwner => ownerUid != 0;

        internal bool IsCurrent()
        {
            if (!prepared || Item.Uid != Uid || !ReferenceEquals(Item.RelicAttributes, attributes) ||
                attributes.Length != 5 || Item.EndDate != endDate || Item.Activate != activate ||
                !ReferenceEquals(Item.Deposite, Deposits.Map) || !Deposits.IsCurrent() ||
                Item.PerfectionLevel != level || Item.PerfectionProgress != progress || Item.OwnerUID != ownerUid ||
                !string.Equals(Item.OwnerName, name, StringComparison.Ordinal) ||
                !string.Equals(Item.Signature, signature, StringComparison.Ordinal)) return false;
            var current = default(LegacyNativeClientItemRecord);
            _ = current.GetDBItem(Item);
            current.RemainingTime = 0;
            return NativeBytes(ref current).SequenceEqual(NativeBytes(ref normalized));
        }

        internal void WriteRecord(Stream destination) => destination.Write(NativeBytes(ref wire));

        internal void WritePerfection(Stream destination)
        {
            if (name is null || signature is null || name.Length > 32 || signature.Length > 32)
                throw InvalidGraph("invalidPerfectionText");
            // Bounded genuine fixed32 factory; never a fake item or accepting writer.
            var record = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(Item);
            destination.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1)));
        }
    }

    private static ReadOnlySpan<byte> NativeBytes(ref LegacyNativeClientItemRecord record) =>
        MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref record, 1));
}
