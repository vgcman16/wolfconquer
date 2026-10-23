using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRuneTable
{
    // Native RuneTable66..82; no clear, skip, catch, normalization or replacement.
    public static void LoadRuneEXP()
    {
        if (File.Exists(LegacyNativeServerConfig.DbLocation + "rune_levexp.txt"))
        {
            string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "rune_levexp.txt");
            string[] array2 = array;
            foreach (string text in array2)
            {
                string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
                RuneLevelEXP runeLevelEXP = new RuneLevelEXP();
                runeLevelEXP.ItemType = Convert.ToUInt32(array3[1]);
                runeLevelEXP.Level = Convert.ToByte(array3[2]);
                runeLevelEXP.RequiredProgress = Convert.ToUInt32(array3[3]);
                RuneLevelEXPList.Add(Convert.ToUInt32(array3[0]), runeLevelEXP);
            }
        }
    }

    // Native RuneTable115..137; all three physical slots are parsed before publication.
    public static void LoadRuneAttributes()
    {
        if (!File.Exists(LegacyNativeServerConfig.DbLocation + "rune_storage_attr.txt"))
        {
            return;
        }
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "rune_storage_attr.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            Attribute attribute = new Attribute();
            attribute.Score = Convert.ToUInt32(array3[1]);
            attribute.AttributeValue = new AttributeValue[3];
            for (int j = 0; j < attribute.AttributeValue.Length; j++)
            {
                attribute.AttributeValue[j] = new AttributeValue();
                attribute.AttributeValue[j].Type = (RuneAttribute)Convert.ToByte(array3[j * 2 + 2]);
                attribute.AttributeValue[j].Value = Convert.ToUInt32(array3[j * 2 + 3]);
            }
            Attributes.Add(Convert.ToUInt32(array3[0]), attribute);
        }
    }

    // Native RuneTable139..162, including the last-decimal-digit dwParam convention.
    public static void LoadRandoms()
    {
        if (!File.Exists(LegacyNativeServerConfig.DbLocation + "xuanbao_rand_attr.txt"))
        {
            return;
        }
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "xuanbao_rand_attr.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            RandomAttribute randomAttribute = new RandomAttribute();
            randomAttribute.ItemID = Convert.ToUInt32(array3[0]);
            randomAttribute.Attribute = (LegacyNativeChiAttribute)Convert.ToByte(array3[1]);
            randomAttribute.Min = Convert.ToUInt32(array3[2]);
            randomAttribute.Max = Convert.ToUInt32(array3[3]);
            if (randomAttribute.Max % 10 == 1)
            {
                randomAttribute.Max--;
                randomAttribute.dwParam = true;
            }
            RandomAttributes.Add(randomAttribute);
        }
    }

    public static void Load()
    {
        LoadRuneAttributes();
        LoadRuneEXP();
        LoadRandoms();
    }
}
