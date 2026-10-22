#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool AddWeapon(LegacyNativeHundredWeaponInfo item, bool Update = false)
    {
        if (ValidWeapon(item.WeaponSubtype, item.Level))
        {
            if (!ContainsWeapon(item.WeaponSubtype))
            {
                Objects.Add(item.WeaponSubtype, item);
            }
            else
            {
                Objects[item.WeaponSubtype] = item;
            }
            ValidateSpells();
            if (Update)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                Owner.Send(stream.CreateHundredWeaponsInfo(Owner, LegacyNativeMsgHundredWeaponsInfo.ActionID.RequestInfo));
            }
            UpdateRank();
            return true;
        }
        UpdateRank();
        return false;
    }

    public bool AddWeapon(LegacyNativeMagicType.WeaponsType subType, byte Level, bool Update = false)
    {
        LegacyNativeHundredWeaponInfo item = new LegacyNativeHundredWeaponInfo
        {
            WeaponSubtype = subType,
            Level = Level,
            BitVector = new LegacyNativeBitVector32(32),
            Attributes = new Dictionary<LegacyNativeHundredWeaponsTable.AttributeType, int>
            {
                { LegacyNativeHundredWeaponsTable.AttributeType.Hitpoints, 0 },
                { LegacyNativeHundredWeaponsTable.AttributeType.PhysicalAttack, 0 },
                { LegacyNativeHundredWeaponsTable.AttributeType.PhysicalDefense, 0 },
                { LegacyNativeHundredWeaponsTable.AttributeType.MagicAttack, 0 },
                { LegacyNativeHundredWeaponsTable.AttributeType.MagicDefense, 0 }
            }
        };
        return AddWeapon(item, Update);
    }

    public bool RemoveWeapon(LegacyNativeMagicType.WeaponsType subType, bool Update = false)
    {
        if (ContainsWeapon(subType))
        {
            if (Objects[subType].DBSpell != null && !Owner.MySpells.ClientSpells.ContainsKey(Objects[subType].DBSpell.ID))
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                Owner.MySpells.Add(stream, Objects[subType].DBSpell.ID, Objects[subType].DBSpell.Level, 0, 0);
            }
            Objects.Remove(subType);
            if (Update)
            {
                using LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream2 = recycledPacket2.GetStream();
                Owner.Send(stream2.CreateHundredWeaponsInfo(Owner, LegacyNativeMsgHundredWeaponsInfo.ActionID.RequestInfo));
            }
            LegacyNativeHWRank.Ranks[LegacyNativeHWRank.GetIndex(subType)].Remove(Owner.Player.UID);
            LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].Remove(Owner.Player.UID);
        }
        return true;
    }

    public bool ClearWeapons(bool Update = false)
    {
        // The held Framework Dictionary invalidates the next MoveNext even after its
        // last removal; .NET 9 does not. Restore that observed own-method boundary.
        Dictionary<LegacyNativeMagicType.WeaponsType, LegacyNativeHundredWeaponInfo> enumeratedObjects = Objects;
        var enumerator = enumeratedObjects.Values.GetEnumerator();
        int originalCount = enumeratedObjects.Count;
        try
        {
            while (true)
            {
                if (enumeratedObjects.Count < originalCount)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }
                if (!enumerator.MoveNext())
                {
                    break;
                }
                LegacyNativeHundredWeaponInfo value = enumerator.Current;
                RemoveWeapon(value.WeaponSubtype);
            }
        }
        finally
        {
            enumerator.Dispose();
        }
        if (Update)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            Owner.Send(stream.CreateHundredWeaponsInfo(Owner, LegacyNativeMsgHundredWeaponsInfo.ActionID.RequestInfo));
        }
        LegacyNativeHWRank.Remove(Owner.Player.UID);
        return true;
    }
}
