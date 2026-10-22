#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native Database.HundredWeapons metadata owner/loader; no startup invocation.
internal class LegacyNativeHundredWeaponsTable
{
    public enum AttributeType : byte
    {
        None,
        Hitpoints,
        PhysicalAttack,
        PhysicalDefense,
        MagicAttack,
        MagicDefense
    }

    public class HundredWeapon
    {
        public LegacyNativeMagicType.WeaponsType WeaponSubtype;
        public byte Level;
        public byte WeaponProf;
        public uint Progress;
        public Dictionary<AttributeType, int> Attributes;
        public ushort MagicType;
        public byte ReqItemCount;
        public bool CanPolish;
    }

    public static Dictionary<uint, HundredWeapon> HundredWeaponsList = new Dictionary<uint, HundredWeapon>();

    public static void Load()
    {
        if (!File.Exists(LegacyNativeServerConfig.DbLocation + "hundred_weapon.txt"))
        {
            return;
        }
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "hundred_weapon.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            string[] array3 = text.Split(new string[1] { "@@" }, StringSplitOptions.RemoveEmptyEntries);
            HundredWeapon hundredWeapon = new HundredWeapon();
            hundredWeapon.WeaponSubtype = (LegacyNativeMagicType.WeaponsType)Convert.ToUInt16(array3[1]);
            hundredWeapon.Level = Convert.ToByte(array3[2]);
            hundredWeapon.WeaponProf = Convert.ToByte(array3[3]);
            hundredWeapon.Progress = Convert.ToUInt32(array3[4]);
            hundredWeapon.Attributes = new Dictionary<AttributeType, int>();
            for (int j = 0; j < 3; j++)
            {
                hundredWeapon.Attributes.Add(unchecked((AttributeType)(Convert.ToInt32(array3[5 + j]) / 10000)),
                    Convert.ToInt32(array3[5 + j]) % 10000);
            }
            hundredWeapon.Attributes.Add(AttributeType.MagicAttack, 0);
            hundredWeapon.Attributes.Add(AttributeType.MagicDefense, 0);
            hundredWeapon.MagicType = Convert.ToUInt16(array3[8]);
            hundredWeapon.ReqItemCount = Convert.ToByte(array3[9]);
            hundredWeapon.CanPolish = Convert.ToByte(array3[10]) == 1;
            HundredWeaponsList.Add(Convert.ToUInt32(array3[0]), hundredWeapon);
        }
    }
}
