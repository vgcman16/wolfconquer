#nullable disable

using System;
using ProtoBuf;

namespace WolfConquer.Core.World;

// Native mutable message/builders using the genuine object-typed packet owner.
// CQ table registration is separate from host startup adoption.
internal class LegacyNativeMsgCombatGearSkin
{
    [Flags]
    public enum TypeID
    {
        Gloryfate = 1,
        HolyOmen = 2
    }

    [ProtoContract]
    public class WeaponInfo
    {
        public uint ID;

        public uint Data1;

        public uint Data2;

        public uint Data3;

        public uint Data4;

        public uint Data5;

        public uint Data6;

        public uint Data7;

        public TypeID Type;
    }

    public WeaponInfo[] Weapon;

    public uint Type;

    public static implicit operator byte[](LegacyNativeMsgCombatGearSkin obj)
    {
        return LegacyNativeCombatGearWire.EncodeObject(obj, 2422);
    }

    public static byte[] Create(LegacyNativeCombatGear.Weapon Item)
    {
        LegacyNativeMsgCombatGearSkin msgCombatGearSkin = new LegacyNativeMsgCombatGearSkin();
        msgCombatGearSkin.Type = 1u;
        msgCombatGearSkin.Weapon = new WeaponInfo[2];
        msgCombatGearSkin.Weapon[0] = new WeaponInfo();
        msgCombatGearSkin.Weapon[0].ID = (uint)Item.ItemID;
        msgCombatGearSkin.Weapon[0].Type = TypeID.Gloryfate;
        msgCombatGearSkin.Weapon[0].Data1 = Item.TypeAnima[0][0];
        msgCombatGearSkin.Weapon[0].Data2 = Item.TypeAnima[0][1];
        msgCombatGearSkin.Weapon[0].Data3 = Item.TypeAnima[0][2];
        msgCombatGearSkin.Weapon[0].Data4 = Item.TypeAnima[0][3];
        msgCombatGearSkin.Weapon[0].Data5 = Item.TypeAnima[0][4];
        msgCombatGearSkin.Weapon[0].Data6 = Item.TypeAnima[0][5];
        msgCombatGearSkin.Weapon[0].Data7 = Item.TypeAnima[0][6];
        msgCombatGearSkin.Weapon[1] = new WeaponInfo();
        msgCombatGearSkin.Weapon[1].ID = (uint)Item.ItemID;
        msgCombatGearSkin.Weapon[1].Type = TypeID.HolyOmen;
        msgCombatGearSkin.Weapon[1].Data1 = Item.TypeAnima[1][0];
        msgCombatGearSkin.Weapon[1].Data2 = Item.TypeAnima[1][1];
        msgCombatGearSkin.Weapon[1].Data3 = Item.TypeAnima[1][2];
        msgCombatGearSkin.Weapon[1].Data4 = Item.TypeAnima[1][3];
        msgCombatGearSkin.Weapon[1].Data5 = Item.TypeAnima[1][4];
        msgCombatGearSkin.Weapon[1].Data6 = Item.TypeAnima[1][5];
        msgCombatGearSkin.Weapon[1].Data7 = Item.TypeAnima[1][6];
        return msgCombatGearSkin;
    }

    public static byte[] Create(LegacyNativeCombatGear info)
    {
        LegacyNativeMsgCombatGearSkin msgCombatGearSkin = new LegacyNativeMsgCombatGearSkin();
        msgCombatGearSkin.Type = 0u;
        int num = 0;
        msgCombatGearSkin.Weapon = new WeaponInfo[info.Items.Values.Count * 2];
        foreach (LegacyNativeCombatGear.Weapon value in info.Items.Values)
        {
            msgCombatGearSkin.Weapon[num] = new WeaponInfo();
            msgCombatGearSkin.Weapon[num].ID = (uint)value.ItemID;
            msgCombatGearSkin.Weapon[num].Type = TypeID.Gloryfate;
            msgCombatGearSkin.Weapon[num].Data1 = value.TypeAnima[0][0];
            msgCombatGearSkin.Weapon[num].Data2 = value.TypeAnima[0][1];
            msgCombatGearSkin.Weapon[num].Data3 = value.TypeAnima[0][2];
            msgCombatGearSkin.Weapon[num].Data4 = value.TypeAnima[0][3];
            msgCombatGearSkin.Weapon[num].Data5 = value.TypeAnima[0][4];
            msgCombatGearSkin.Weapon[num].Data6 = value.TypeAnima[0][5];
            msgCombatGearSkin.Weapon[num].Data7 = value.TypeAnima[0][6];
            num++;
            msgCombatGearSkin.Weapon[num] = new WeaponInfo();
            msgCombatGearSkin.Weapon[num].ID = (uint)value.ItemID;
            msgCombatGearSkin.Weapon[num].Type = TypeID.HolyOmen;
            msgCombatGearSkin.Weapon[num].Data1 = value.TypeAnima[1][0];
            msgCombatGearSkin.Weapon[num].Data2 = value.TypeAnima[1][1];
            msgCombatGearSkin.Weapon[num].Data3 = value.TypeAnima[1][2];
            msgCombatGearSkin.Weapon[num].Data4 = value.TypeAnima[1][3];
            msgCombatGearSkin.Weapon[num].Data5 = value.TypeAnima[1][4];
            msgCombatGearSkin.Weapon[num].Data6 = value.TypeAnima[1][5];
            msgCombatGearSkin.Weapon[num].Data7 = value.TypeAnima[1][6];
            num++;
        }
        return msgCombatGearSkin;
    }
}
