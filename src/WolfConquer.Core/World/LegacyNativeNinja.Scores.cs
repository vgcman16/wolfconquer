#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public uint Score => unchecked(Fire_Score + Water_Score + Earth_Score + Wind_Score + Lightning_Score + a_Score);

    public uint Fire_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 100 && p.ItemID <= 105))
                {
                    num += item.DBItem.Score + Fire * 4;
                }
                return num + FireMastery;
            }
        }
    }

    public uint Water_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 200 && p.ItemID <= 205))
                {
                    num += item.DBItem.Score + Water * 4;
                }
                return num + WaterMastery;
            }
        }
    }

    public uint Earth_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 300 && p.ItemID <= 305))
                {
                    num += item.DBItem.Score + Earth * 4;
                }
                return num + EarthMastery;
            }
        }
    }

    public uint Wind_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 400 && p.ItemID <= 405))
                {
                    num += item.DBItem.Score + Wind * 4;
                }
                return num + WindMastery;
            }
        }
    }

    public uint Lightning_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 500 && p.ItemID <= 505))
                {
                    num += item.DBItem.Score + Lightning * 4;
                }
                return num + LightningMastery;
            }
        }
    }

    public uint a_Score
    {
        get
        {
            unchecked
            {
                uint num = 0u;
                foreach (Item item in Items.Values.Where((Item p) => p.ItemID >= 600 && p.ItemID <= 605))
                {
                    num += item.DBItem.Score;
                }
                return num;
            }
        }
    }
}
