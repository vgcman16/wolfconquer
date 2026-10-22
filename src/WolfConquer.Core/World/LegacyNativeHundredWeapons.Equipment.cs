#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public bool Equip(LegacyNativeMagicType.WeaponsType WeaponSubType, int Position, bool update = true)
    {
        if (Objects.ContainsKey(WeaponSubType))
        {
            if (!Objects[WeaponSubType].BitVector.Contain(Position))
            {
                Objects[WeaponSubType].BitVector.Add(Position);
                if (update)
                {
                    using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    Owner.Send(stream.CreateHundredWeaponsOpt(new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
                    {
                        Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Equip,
                        WeaponSubtype = WeaponSubType,
                        Position = Position
                    }));
                }
            }
            return true;
        }
        return false;
    }

    public bool Show(LegacyNativeMagicType.WeaponsType WeaponSubType, byte Position)
    {
        if (Objects.ContainsKey(WeaponSubType))
        {
            if (Objects[WeaponSubType].AppearancePosition != Position && Objects[WeaponSubType].Level >= 3)
            {
                Objects[WeaponSubType].AppearancePosition = Position;
            }
            return true;
        }
        return false;
    }

    public bool Unequip(int Position, bool update = true)
    {
        foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
        {
            if (!value.BitVector.Contain(Position))
            {
                continue;
            }
            value.BitVector.Remove(Position);
            if (update)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                Owner.Send(stream.CreateHundredWeaponsOpt(new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
                {
                    Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Unequip,
                    Position = Position
                }));
            }
            return true;
        }
        return false;
    }

    public bool Hide(byte Position)
    {
        foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
        {
            if (value.AppearancePosition == Position)
            {
                value.AppearancePosition = 0;
                return true;
            }
        }
        return false;
    }

    public bool Hide(LegacyNativeMagicType.WeaponsType subtype)
    {
        foreach (LegacyNativeHundredWeaponInfo value in Objects.Values)
        {
            if (value.WeaponSubtype == subtype)
            {
                value.AppearancePosition = 0;
                return true;
            }
        }
        return false;
    }

    public bool Swap(int Position1, int Position2, bool update = true)
    {
        if (!Free(Position1) && !Free(Position2))
        {
            LegacyNativeHundredWeaponInfo hundredWeaponInfo = TryGetItem(Position1);
            LegacyNativeHundredWeaponInfo hundredWeaponInfo2 = TryGetItem(Position2);
            hundredWeaponInfo.BitVector.Remove(Position1);
            hundredWeaponInfo2.BitVector.Remove(Position2);
            hundredWeaponInfo.BitVector.Add(Position2);
            hundredWeaponInfo2.BitVector.Add(Position1);
            if (update)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                Owner.Send(stream.CreateHundredWeaponsOpt(new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
                {
                    Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Swap,
                    Exchange = new LegacyNativeMsgHundredWeaponsOpt.ExchangePositions
                    {
                        Position1 = Position1,
                        Position2 = Position2
                    }
                }));
            }
            return true;
        }
        return false;
    }
}
