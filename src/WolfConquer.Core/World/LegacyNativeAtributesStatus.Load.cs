namespace WolfConquer.Core.World;

internal partial class LegacyNativeAtributesStatus
{
    public void Load()
    {
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "Stats.ini");
        string[] array2 = array;
        foreach (string text in array2)
        {
            // Only identical branch mechanics are shared. The native ordered, current-culture
            // StartsWith calls and fixed Remove offsets are retained, without header cleanup.
            uint family;
            int offset;
            if (text.StartsWith("Monk"))
            {
                family = 6005u;
                offset = 5;
            }
            else if (text.StartsWith("Archer"))
            {
                family = 4005u;
                offset = 7;
            }
            else if (text.StartsWith("Ninja"))
            {
                family = 5005u;
                offset = 6;
            }
            else if (text.StartsWith("Taoist"))
            {
                family = 10000u;
                offset = 7;
            }
            else if (text.StartsWith("Trojan"))
            {
                family = 1005u;
                offset = 7;
            }
            else if (text.StartsWith("Warrior"))
            {
                family = 2005u;
                offset = 8;
            }
            else if (text.StartsWith("Pirate"))
            {
                family = 7005u;
                offset = 7;
            }
            else if (text.StartsWith("LongLee"))
            {
                family = 8005u;
                offset = 8;
            }
            else if (text.StartsWith("WindWalker"))
            {
                family = 16005u;
                offset = 11;
            }
            else if (text.StartsWith("Thunderstriker"))
            {
                family = 9005u;
                offset = 15;
            }
            else
            {
                continue;
            }
            string[] array3 = text.Split(']');
            byte key = byte.Parse(array3[0].Remove(0, offset));
            string text2 = array3[1].Remove(0, 1);
            string[] array4 = text2.Split(',');
            Instant instant = new Instant();
            instant.Strenght = ushort.Parse(array4[0]);
            instant.Vitality = ushort.Parse(array4[1]);
            instant.Agility = ushort.Parse(array4[2]);
            instant.Spirit = ushort.Parse(array4[3]);
            if (ContainsKey(family))
            {
                base[family].Add(key, instant);
                continue;
            }
            Dictionary<ushort, Instant> dictionary = new Dictionary<ushort, Instant>();
            dictionary.Add(key, instant);
            Add(family, dictionary);
        }
    }
}
