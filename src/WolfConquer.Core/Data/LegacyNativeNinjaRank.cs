#nullable disable

namespace WolfConquer.Core.Data;

// Complete native NinjaRank owner. Storage, appearance and persistence are split by method family.
// No server lifecycle, rank receiver or managed world binding is installed.
internal partial class LegacyNativeNinjaRank
{
    public enum Type : uint
    {
        Overall,
        Fire,
        Water,
        Earth,
        Wind,
        Lightning,
        Count
    }

    public partial class Entry
    {
        public Type Type;
        public uint UID;
        public string Name;
        public uint Class;
        public byte Level;
        public uint Mesh;
        public uint TotalPoints;
        public uint HairStyle;
        public uint Head;
        public uint Garment;
        public uint LeftWeapon;
        public uint LefttWeaponAccessory;
        public uint RightWeapon;
        public uint RightWeaponAccessory;
        public uint MountArmor;
        public uint Armor;
        public uint Wing;
        public uint WingPlus;
        public uint Title;
        public uint Flag;
        public string GuildName = "";

        public Entry ShallowCopy()
        {
            return (Entry)MemberwiseClone();
        }
    }
}
