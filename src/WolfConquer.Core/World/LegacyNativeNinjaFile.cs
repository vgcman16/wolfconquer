#nullable disable

namespace WolfConquer.Core.World;

// Native NinjaFile table rows, initially-null static owners and complete Load.
// SigilPacks NPC exchange and startup/host publication remain wholly absent.
internal partial class LegacyNativeNinjaFile
{
    public class NinjaSprintItem
    {
        public uint ID;
        public uint ItemID;
        public uint Level;
        public uint Exp;
        public uint UK1;
        public uint UK2;
        public uint magic1;
        public uint magic2;
        public int Power;
        public uint Damage;
        public uint Score;
        public uint UK5;
    }

    public class Item
    {
        public uint Level;
        public uint U1;
        public uint MaxHP;
        public uint PAttack;
        public uint PDefense;
        public uint MDefense;
        public uint Chance;
        public uint Seconds;
        public uint Rate;
        public uint MovementSpeed;
        public uint U13;
        public uint U14;
        public uint P;
    }

    public static Dictionary<uint, NinjaSprintItem> gouyu_type;
    public static Dictionary<uint, Item> gouyu_immortals;
}
