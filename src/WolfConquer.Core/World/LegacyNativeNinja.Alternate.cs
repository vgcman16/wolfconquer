#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void Alternate()
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        List<ushort> list = new List<ushort>
        {
            16310, 16320, 16330, 16340, 16350, 16360, 16370, 16380, 16390, 16400,
            16410, 16420, 16430, 16440, 16450, 16460, 16470, 16480, 16490, 16500,
            16510, 16520, 16530, 16540, 16550
        };
        unchecked
        {
            if (user.Equipment.Alternante)
            {
                int i;
                for (i = 9; i < 17; i++)
                {
                    Item item = Items.Values.Where((Item p) => p.Position == i).FirstOrDefault();
                    if (item != null)
                    {
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item.DBItem.magic1))
                        {
                            user.MySpells.Add(stream, (ushort)item.DBItem.magic1, (byte)item.Level, 0, 0);
                        }
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item.DBItem.magic2))
                        {
                            user.MySpells.Add(stream, (ushort)item.DBItem.magic2, (byte)item.Level, 0, 0);
                        }
                        if (list.Contains((ushort)item.DBItem.magic1))
                        {
                            list.Remove((ushort)item.DBItem.magic1);
                        }
                        if (list.Contains((ushort)item.DBItem.magic2))
                        {
                            list.Remove((ushort)item.DBItem.magic2);
                        }
                        continue;
                    }
                    item = Items.Values.Where((Item p) => p.Position == i - 8).FirstOrDefault();
                    if (item != null)
                    {
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item.DBItem.magic1))
                        {
                            user.MySpells.Add(stream, (ushort)item.DBItem.magic1, (byte)item.Level, 0, 0);
                        }
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item.DBItem.magic2))
                        {
                            user.MySpells.Add(stream, (ushort)item.DBItem.magic2, (byte)item.Level, 0, 0);
                        }
                        if (list.Contains((ushort)item.DBItem.magic1))
                        {
                            list.Remove((ushort)item.DBItem.magic1);
                        }
                        if (list.Contains((ushort)item.DBItem.magic2))
                        {
                            list.Remove((ushort)item.DBItem.magic2);
                        }
                    }
                }
            }
            else
            {
                int i2;
                for (i2 = 1; i2 < 9; i2++)
                {
                    Item item2 = Items.Values.Where((Item p) => p.Position == i2).FirstOrDefault();
                    if (item2 != null)
                    {
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item2.DBItem.magic1))
                        {
                            user.MySpells.Add(stream, (ushort)item2.DBItem.magic1, (byte)item2.Level, 0, 0);
                        }
                        if (!user.MySpells.ClientSpells.ContainsKey((ushort)item2.DBItem.magic2))
                        {
                            user.MySpells.Add(stream, (ushort)item2.DBItem.magic2, (byte)item2.Level, 0, 0);
                        }
                        if (list.Contains((ushort)item2.DBItem.magic1))
                        {
                            list.Remove((ushort)item2.DBItem.magic1);
                        }
                        if (list.Contains((ushort)item2.DBItem.magic2))
                        {
                            list.Remove((ushort)item2.DBItem.magic2);
                        }
                    }
                }
            }
            foreach (ushort item3 in list)
            {
                if (user.MySpells.ClientSpells.ContainsKey(item3))
                {
                    user.MySpells.Remove(item3, stream);
                }
            }
        }
    }
}
