using System.Buffers.Binary;
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Physical GameClient-owned RAW-LOAD slice, NOT full native Equip/MsgGameItem or live weapon admission.
// The mutable native public Add/Remove/Inventory/counter producers are deliberately not implemented here.
internal sealed class LegacyNativeEquipmentRuntime
{
    private readonly ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem> clientItems = new();
    private int invalidated;
    private bool loadCompleted;
    private string? unavailableReason;

    internal LegacyNativeEquipmentRuntime(LegacyNativeGameClientRuntime owner) => Owner = owner;
    internal LegacyNativeGameClientRuntime Owner { get; }
    internal bool CanAuthorizeWeaponAdmission => false; // Capability boundary, NOT a native field.

    internal void InvalidateRawLoadObservation(string reason)
    {
        Interlocked.Exchange(ref invalidated, 1); // Monotonic even if a load and unsupported mutation overlap.
        unavailableReason ??= reason;
    }

    internal LegacyNativeEquipmentRawObservation ObserveRawLoadedPosition(ushort position)
    {
        // Never return "empty equipment" when the raw observation is stale/unsupported/incomplete.
        if (Volatile.Read(ref invalidated) != 0 || !loadCompleted)
            return new(false, false, null, [], unavailableReason ?? "rawLoadNotCompleted");
        var item = clientItems.Values.Where(item => item.Position == position).FirstOrDefault();
        return new(true, item is not null, item, clientItems.ToArray(), null);
    }

    internal void CaptureRawLoad(byte[]? bytes)
    {
        if (loadCompleted || clientItems.Count != 0)
        {
            InvalidateRawLoadObservation("repeatedRawLoad");
            return;
        }
        if (bytes is null) { loadCompleted = true; return; } // Configured file missing: native Open returns.
        try
        {
            DecodeRawLoad(bytes);
            loadCompleted = true;
        }
        catch (Exception error) when (error is FormatException or OverflowException or NotSupportedException)
        {
            // Preserve the existing DTO loader's separate behavior; expose NO valid native observation.
            // Partial physical entries are retained, not rolled back, but cannot authorize a lookup.
            InvalidateRawLoadObservation(error.GetType().Name + ":" + error.Message);
        }
    }

    private void DecodeRawLoad(ReadOnlySpan<byte> bytes)
    {
        var offset = 0;
        var count = ReadCount(bytes, ref offset);
        for (var index = 0; index < count; index++)
        {
            var route = ReadRow(bytes, ref offset);
            if (route.ItemId == 205_901) continue; // Source continues BEFORE reading this row's deposits.
            var parent = ConvertItem(route);
            var deposits = route.DepositCount; // Saved before the SAME native DBItem variable is overwritten.
            for (uint deposit = 0; deposit < deposits; deposit++)
            {
                route = ReadRow(bytes, ref offset);
                var child = ConvertItem(route);
                parent.Deposits.TryAdd(child.Uid, child);
            }

            // Source1244..1280 routes/keys using LAST DBItem, but VALUE stays parent dataItem.
            // Thus dictionary key != value.Uid and routing position != value.Position are lawful.
            if (route.WarehouseId != 0)
            {
                if (route.WarehouseId == 100 && route.Position is > 0 and <= 59)
                    clientItems.TryAdd(route.Uid, parent);
                continue;
            }
            if (route.Position is >= 213 and <= 215) continue; // RelicFuse branch precedes Equipment.
            if (route.Position is > 0 and <= 63) clientItems.TryAdd(route.Uid, parent);
        }

        // Full perfection/hydration effects are not this three-field lookup slice.
        // Validate framing without pretending the ignored postlude creates another item generation.
        var perfectionCount = ReadCount(bytes, ref offset);
        var tail = checked(perfectionCount * 80);
        Ensure(bytes, offset, tail);
        offset += tail;
        if (offset != bytes.Length) throw new FormatException("raw item trailing bytes");
    }

    private static LegacyNativeEquipmentRawItem ConvertItem(RawRow row)
    {
        // Native ITEM_ID setter performs Pool/Rune effects for these IDs. They are an explicit
        // unavailable observation until the genuine constructor dependency is ported, NOT guessed.
        if (row.ItemId / 10_000 is >= 401 and <= 403)
            throw new NotSupportedException("native rune item constructor not implemented");
        return new(row.Uid, row.ItemId, row.Position);
    }

    private static RawRow ReadRow(ReadOnlySpan<byte> bytes, ref int offset)
    {
        Ensure(bytes, offset, 184);
        var row = bytes.Slice(offset, 184);
        offset += 184;
        return new(BinaryPrimitives.ReadUInt32LittleEndian(row),
            BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(4)),
            BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(12)),
            BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(56)),
            BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(136)));
    }

    private static int ReadCount(ReadOnlySpan<byte> bytes, ref int offset)
    {
        Ensure(bytes, offset, 4);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
        offset += 4;
        if (count < 0) throw new FormatException("negative raw item count");
        return count;
    }

    private static void Ensure(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
            throw new FormatException("truncated raw item input");
    }

    private readonly record struct RawRow(
        uint Uid, uint ItemId, ushort Position, uint WarehouseId, uint DepositCount);
}

// A distinct physical reference created from each raw GetDataItem-equivalent selected field set.
// NOT a DTO alias, dictionary key, UID cache, full MsgGameItem or an implemented public mutation writer.
internal sealed class LegacyNativeEquipmentRawItem(uint uid, uint itemId, ushort position)
{
    internal uint Uid { get; } = uid;
    internal uint ItemId { get; } = itemId;
    internal ushort Position { get; } = position;
    internal ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem> Deposits { get; } = new();
}

internal sealed record LegacyNativeEquipmentRawObservation(
    bool Available, bool Found, LegacyNativeEquipmentRawItem? Item,
    IReadOnlyList<KeyValuePair<uint, LegacyNativeEquipmentRawItem>> RawEntries, string? UnavailableReason);
