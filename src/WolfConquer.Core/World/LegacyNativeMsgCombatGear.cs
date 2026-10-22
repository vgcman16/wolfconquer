#nullable disable

using System;

namespace WolfConquer.Core.World;

// Literal native mutable message/builders; only the registered typed wire projection is used.
internal class LegacyNativeMsgCombatGear
{
    [Flags]
    public enum TypeID
    {
        Load = 0,
        View = 1,
        Add = 2,
        Update = 3,
        CrossServer = 0xA
    }

    public class Item
    {
        public uint Transform = 0u;

        public uint Level = 0u;

        public uint Exp = 0u;

        public LegacyNativeCombatGear.TypeID ID = LegacyNativeCombatGear.TypeID.None;

        public uint Equip = 0u;

        public uint Data1;

        public uint Data2;

        public uint Data3;

        public uint Data4;

        public uint Data5;

        public uint Data6;

        public uint MasteryScore;
    }

    public Item[] Items;

    public uint UID = 0u;

    public TypeID Type = TypeID.Load;

    public static implicit operator byte[](LegacyNativeMsgCombatGear obj)
    {
        return LegacyNativeCombatGearWire.Encode(obj, 2420);
    }

    public static byte[] Create(uint UserID, LegacyNativeCombatGear.Weapon Item, TypeID Type)
    {
        LegacyNativeMsgCombatGear msgCombatGear = new LegacyNativeMsgCombatGear();
        msgCombatGear.Type = Type;
        msgCombatGear.UID = UserID;
        msgCombatGear.Items = new Item[1];
        msgCombatGear.Items[0] = new Item();
        msgCombatGear.Items[0].ID = Item.ItemID;
        msgCombatGear.Items[0].Level = Item.Level;
        msgCombatGear.Items[0].Exp = Item.Exp;
        msgCombatGear.Items[0].Equip = Item.Equip;
        msgCombatGear.Items[0].Transform = Item.Transform;
        msgCombatGear.Items[0].Data1 = Item.BlackJades[0].Type;
        msgCombatGear.Items[0].Data2 = Item.BlackJades[1].Type;
        msgCombatGear.Items[0].Data3 = Item.BlackJades[2].Type;
        msgCombatGear.Items[0].Data4 = Item.BlackJades[3].Type;
        msgCombatGear.Items[0].Data5 = Item.BlackJades[4].Type;
        msgCombatGear.Items[0].Data6 = Item.BlackJades[5].Type;
        msgCombatGear.Items[0].MasteryScore = Item.MasteryScore;
        return msgCombatGear;
    }

    public static byte[] Create(LegacyNativeCombatGear info, TypeID Type, uint UID, uint Class)
    {
        LegacyNativeMsgCombatGear msgCombatGear = new LegacyNativeMsgCombatGear();
        msgCombatGear.Type = Type;
        msgCombatGear.UID = UID;
        msgCombatGear.Items = new Item[info.GetItems(Class).Length];
        int num = 0;
        LegacyNativeCombatGear.Weapon[] items = info.GetItems(Class);
        foreach (LegacyNativeCombatGear.Weapon weapon in items)
        {
            if (weapon != null)
            {
                msgCombatGear.Items[num] = new Item();
                msgCombatGear.Items[num].ID = weapon.ItemID;
                msgCombatGear.Items[num].Level = weapon.Level;
                msgCombatGear.Items[num].Exp = weapon.Exp;
                msgCombatGear.Items[num].Equip = weapon.Equip;
                msgCombatGear.Items[num].Transform = weapon.Transform;
                msgCombatGear.Items[num].Data1 = weapon.BlackJades[0].Type;
                msgCombatGear.Items[num].Data2 = weapon.BlackJades[1].Type;
                msgCombatGear.Items[num].Data3 = weapon.BlackJades[2].Type;
                msgCombatGear.Items[num].Data4 = weapon.BlackJades[3].Type;
                msgCombatGear.Items[num].Data5 = weapon.BlackJades[4].Type;
                msgCombatGear.Items[num].Data6 = weapon.BlackJades[5].Type;
                msgCombatGear.Items[num].MasteryScore = weapon.MasteryScore;
                num++;
            }
        }
        return msgCombatGear;
    }
}
