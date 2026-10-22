#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinjaFile
{
    // Complete native ElTabakh/NinjaFile.cs564..620; no startup caller installed.
    public static void Load()
    {
        gouyu_type = new Dictionary<uint, NinjaSprintItem>();
        if (!File.Exists(LegacyNativeServerConfig.DbLocation + "gouyu_type.txt"))
        {
            Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "gouyu_type.txt");
            return;
        }
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "gouyu_type.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            NinjaSprintItem ninjaSprintItem = new NinjaSprintItem();
            ninjaSprintItem.ID = Convert.ToUInt32(array3[0]);
            ninjaSprintItem.ItemID = Convert.ToUInt32(array3[1]);
            ninjaSprintItem.Level = Convert.ToUInt32(array3[2]);
            ninjaSprintItem.Exp = Convert.ToUInt32(array3[3]);
            ninjaSprintItem.UK1 = Convert.ToUInt32(array3[4]);
            ninjaSprintItem.UK2 = Convert.ToUInt32(array3[5]);
            ninjaSprintItem.magic1 = Convert.ToUInt32(array3[6]);
            ninjaSprintItem.magic2 = Convert.ToUInt32(array3[7]);
            ninjaSprintItem.Power = Convert.ToInt32(array3[8]);
            ninjaSprintItem.Damage = Convert.ToUInt32(array3[9]);
            ninjaSprintItem.Score = Convert.ToUInt32(array3[11]);
            ninjaSprintItem.UK5 = Convert.ToUInt32(array3[11]);
            gouyu_type.Add(ninjaSprintItem.ID, ninjaSprintItem);
        }
        gouyu_immortals = new Dictionary<uint, Item>();
        if (!File.Exists(LegacyNativeServerConfig.DbLocation + "gouyu_immortal.txt"))
        {
            Console.WriteLine("!File.Exists: " + LegacyNativeServerConfig.DbLocation + "gouyu_immortal.txt");
            return;
        }
        array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "gouyu_immortal.txt");
        string[] array4 = array;
        foreach (string text2 in array4)
        {
            string[] array5 = text2.Split(new string[2] { "@@", " " }, StringSplitOptions.RemoveEmptyEntries);
            Item item = new Item();
            item.Level = Convert.ToUInt32(array5[0]);
            item.U1 = Convert.ToUInt32(array5[1]);
            item.MaxHP = Convert.ToUInt32(array5[2]);
            item.PAttack = Convert.ToUInt32(array5[3]);
            item.PDefense = Convert.ToUInt32(array5[4]);
            item.MDefense = Convert.ToUInt32(array5[5]);
            item.Chance = Convert.ToUInt32(array5[6]);
            item.Seconds = Convert.ToUInt32(array5[7]);
            item.Rate = Convert.ToUInt32(array5[8]);
            item.MovementSpeed = Convert.ToUInt32(array5[12]);
            item.U13 = Convert.ToUInt32(array5[13]);
            item.U14 = Convert.ToUInt32(array5[14]);
            item.P = Convert.ToUInt32(array5[15]);
            gouyu_immortals.Add(item.Level, item);
        }
    }
}
