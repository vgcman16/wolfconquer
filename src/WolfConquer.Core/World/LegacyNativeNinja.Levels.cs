#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public uint Levels
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                if (user.Equipment.Alternante)
                {
                    int i;
                    for (i = 9; i < 17; i++)
                    {
                        Item item = Items.Values.Where((Item p) => p.Position == i).FirstOrDefault();
                        if (item != null)
                        {
                            num += item.Level;
                            continue;
                        }
                        item = Items.Values.Where((Item p) => p.Position == i - 8).FirstOrDefault();
                        if (item != null)
                        {
                            num += item.Level;
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
                            num += item2.Level;
                        }
                    }
                }
                return num;
            }
        }
    }
}
