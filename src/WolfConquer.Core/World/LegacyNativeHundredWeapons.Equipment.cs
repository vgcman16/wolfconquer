using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool Equip(LegacyNativeMagicType.WeaponsType WeaponSubType, int Position, bool update = true)
    {
        lock (slotSyncRoot)
        {
            if (!TryAdmitSlotMutation(Position, out var player, out var rows)) return false;
            uint uid = player.UID;
            var item = rows.FirstOrDefault(row => row.WeaponSubtype == WeaponSubType);
            if (item is null) return false;
            var occupant = rows.FirstOrDefault(row => row.BitVector.Contain(Position));
            if (occupant is not null) return ReferenceEquals(occupant, item);
            using var packet = update ? LegacyNativeHundredWeaponsSlotPacket.TryPrepare(new()
            {
                Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Equip,
                WeaponSubtype = WeaponSubType, Position = Position
            }) : null;
            if ((update && packet is null) || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            item.BitVector.Add(Position);
            packet?.Publish(Owner);
            return true;
        }
    }

    public bool Show(LegacyNativeMagicType.WeaponsType WeaponSubType, byte Position)
    {
        lock (slotSyncRoot)
        {
            if (!TryGetSlotMutationPlayer(out var player) ||
                !TrySnapshotSlotStorage(requireBitmap: true, out var rows)) return false;
            uint uid = player.UID;
            var item = rows.FirstOrDefault(row => row.WeaponSubtype == WeaponSubType);
            if (item is null || item.Level < 3 || rows.Any(row => !ReferenceEquals(row, item) &&
                Position != 0 && row.AppearancePosition == Position) || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            item.AppearancePosition = Position;
            return true;
        }
    }

    public bool Unequip(int Position, bool update = true)
    {
        lock (slotSyncRoot)
        {
            if (!TryAdmitSlotMutation(Position, out var player, out var rows)) return false;
            uint uid = player.UID;
            var item = rows.FirstOrDefault(row => row.BitVector.Contain(Position));
            if (item is null) return false;
            using var packet = update ? LegacyNativeHundredWeaponsSlotPacket.TryPrepare(new()
            {
                Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Unequip, Position = Position
            }) : null;
            if ((update && packet is null) || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            item.BitVector.Remove(Position);
            packet?.Publish(Owner);
            return true;
        }
    }

    public bool Hide(byte Position)
    {
        lock (slotSyncRoot)
        {
            if (!TryGetSlotMutationPlayer(out var player) ||
                !TrySnapshotSlotStorage(requireBitmap: true, out var rows)) return false;
            uint uid = player.UID;
            var item = rows.FirstOrDefault(row => row.AppearancePosition == Position);
            if (item is null || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            item.AppearancePosition = 0;
            return true;
        }
    }

    public bool Hide(LegacyNativeMagicType.WeaponsType subtype)
    {
        lock (slotSyncRoot)
        {
            if (!TryGetSlotMutationPlayer(out var player) ||
                !TrySnapshotSlotStorage(requireBitmap: true, out var rows)) return false;
            uint uid = player.UID;
            var item = rows.FirstOrDefault(row => row.WeaponSubtype == subtype);
            if (item is null || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            item.AppearancePosition = 0;
            return true;
        }
    }

    public bool Swap(int Position1, int Position2, bool update = true)
    {
        lock (slotSyncRoot)
        {
            // Unknown is not occupied: no !Free(...) test is an admission decision.
            if (!IsSlotPosition(Position1) || !TryAdmitSlotMutation(Position2, out var player, out var rows)) return false;
            uint uid = player.UID;
            var left = rows.FirstOrDefault(row => row.BitVector.Contain(Position1));
            var right = rows.FirstOrDefault(row => row.BitVector.Contain(Position2));
            if (left is null || right is null) return false;
            using var packet = update ? LegacyNativeHundredWeaponsSlotPacket.TryPrepare(new()
            {
                Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Swap,
                Exchange = new() { Position1 = Position1, Position2 = Position2 }
            }) : null;
            if ((update && packet is null) || !IsCurrentSlotMutationPlayer(player, uid)) return false;
            left.BitVector.Remove(Position1);
            right.BitVector.Remove(Position2);
            left.BitVector.Add(Position2);
            right.BitVector.Add(Position1);
            packet?.Publish(Owner);
            return true;
        }
    }
}
