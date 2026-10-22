#nullable disable

namespace WolfConquer.Core.Data;

// Complete native HWRank owner; its own catalog is independent of NinjaRank.
// No startup, save scheduler, packet receiver or host consumer is installed.
internal partial class LegacyNativeHWRank
{
    public enum Type : uint
    {
        Main,
        Dominator,
        Judge,
        Fury,
        Doom,
        Soul,
        Power,
        Aurora,
        Dragonfang,
        Faith,
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
