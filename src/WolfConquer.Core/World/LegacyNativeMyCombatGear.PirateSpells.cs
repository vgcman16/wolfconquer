using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Native MyCombatGear154..223. This is NOT Loading/UseRune/attack dispatch.
    public void IsPirate(byte x)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        foreach (Weapon value in user.CombatGear!.Items.Values)
        {
            if (x == 1)
            {
                BlackJade[] blackJades = value.BlackJades;
                foreach (BlackJade blackJade in blackJades)
                {
                    if (blackJade.DBItem.attr_type1 == 24)
                    {
                        ushort iD = (ushort)(blackJade.DBItem.attr_value1 / 1000);
                        ushort level = (ushort)(blackJade.DBItem.attr_value1 % 100);
                        user.MySpells!.Add(stream, iD, level, 0, 0);
                    }
                    if (blackJade.DBItem.attr_type2 == 24)
                    {
                        ushort iD2 = (ushort)(blackJade.DBItem.attr_value2 / 1000);
                        ushort level2 = (ushort)(blackJade.DBItem.attr_value2 % 100);
                        user.MySpells!.Add(stream, iD2, level2, 0, 0);
                    }
                }
                if (value.ItemID >= TypeID.Pirate1 && value.ItemID <= TypeID.Pirate3)
                {
                    uint[] skills = value.DBItem.Skills;
                    foreach (uint num in skills)
                    {
                        if (num != 0)
                            user.MySpells!.Add(stream, (ushort)(num / 1000), (byte)(num % 100), 0, 0);
                    }
                }
                user.MySpells!.Add(stream, 21540, 0, 0, 0);
                user.MySpells!.Add(stream, 21920, 0, 0, 0);
                continue;
            }
            BlackJade[] blackJades2 = value.BlackJades;
            foreach (BlackJade blackJade2 in blackJades2)
            {
                if (blackJade2.DBItem.attr_type1 == 24)
                {
                    ushort iD3 = (ushort)(blackJade2.DBItem.attr_value1 / 1000);
                    ushort num2 = (ushort)(blackJade2.DBItem.attr_value1 % 100);
                    user.MySpells!.Remove(iD3, stream);
                }
                if (blackJade2.DBItem.attr_type2 == 24)
                {
                    ushort iD4 = (ushort)(blackJade2.DBItem.attr_value2 / 1000);
                    ushort num3 = (ushort)(blackJade2.DBItem.attr_value2 % 100);
                    user.MySpells!.Remove(iD4, stream);
                }
            }
            if (value.ItemID >= TypeID.Pirate1 && value.ItemID <= TypeID.Pirate3)
            {
                uint[] skills2 = value.DBItem.Skills;
                foreach (uint num4 in skills2)
                {
                    if (num4 != 0)
                        user.MySpells!.Remove((ushort)(num4 / 1000), stream);
                }
            }
            user.MySpells!.Remove(21540, stream);
            user.MySpells!.Remove(21920, stream);
        }
    }
}
