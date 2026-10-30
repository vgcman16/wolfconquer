using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Physical client-owned RAW-LOAD observation, not genuine Equip/MsgGameItem or
// live weapon admission. Public native mutation/counter producers remain absent.
internal sealed partial class LegacyNativeEquipmentRuntime
{
    private readonly object rawLoadGate = new();
    private ConcurrentDictionary<uint, LegacyNativeEquipmentRawItem> clientItems = new();
    private int invalidated;
    private bool loadCompleted;
    private string? unavailableReason;

    internal LegacyNativeEquipmentRuntime(LegacyNativeGameClientRuntime owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
    }

    internal LegacyNativeGameClientRuntime Owner { get; }
    internal bool CanAuthorizeWeaponAdmission => false; // Capability boundary, NOT a native field.

    internal void InvalidateRawLoadObservation(string reason)
    {
        lock (rawLoadGate) InvalidateRawLoad(reason);
    }

    private void InvalidateRawLoad(string reason)
    {
        Interlocked.Exchange(ref invalidated, 1); // Monotonic for the observer lifetime.
        unavailableReason ??= reason;
    }

    internal LegacyNativeEquipmentRawObservation ObserveRawLoadedPosition(ushort position)
    {
        lock (rawLoadGate)
        {
            // Unavailable is never presented as genuinely empty equipment.
            if (Volatile.Read(ref invalidated) != 0 || !loadCompleted)
                return new(false, false, null, [], unavailableReason ?? "rawLoadNotCompleted");
            var entries = clientItems.ToArray();
            var item = entries.Select(entry => entry.Value).FirstOrDefault(item => item.Position == position);
            return new(true, item is not null, item, entries, null);
        }
    }

    internal void CaptureRawLoad(byte[]? bytes)
    {
        lock (rawLoadGate)
        {
            if (loadCompleted || clientItems.Count != 0)
            {
                InvalidateRawLoad("repeatedRawLoad");
                return;
            }
            if (Volatile.Read(ref invalidated) != 0) return; // No failure/invalidation revival.
            if (bytes is null) { loadCompleted = true; return; } // Configured file missing.
            try
            {
                // All physical parsing, raw graph construction and collection
                // work precede the only nonthrowing publication assignments.
                var prepared = PrepareRawLoad(bytes);
                clientItems = prepared;
                loadCompleted = true;
            }
            catch (Exception error) when (error is FormatException or OverflowException or
                NotSupportedException or InvalidDataException or EndOfStreamException)
            {
                // The DTO/native loaders have their own domain behavior. Expose
                // no valid raw observation and retain no partially prepared rows.
                InvalidateRawLoad(error.GetType().Name + ":" + error.Message);
            }
        }
    }
}

// A concrete selected-field observation, not a DTO alias, UID cache or genuine
// native MsgGameItem. Repeated observations retain the same parent/child aliases.
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
