#nullable disable

using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Native user constructor, full GetEntry25..61, UpdateRank64..67 and both isOpen helpers.
// Loading remains absent; client birth does not publish this owner.
// Private source receiver bodies do not install Packet attributes or a host dispatcher.
internal partial class LegacyNativeMyCombatGear : LegacyNativeCombatGear
{
    public LegacyNativeGameClientRuntime user;

    public LegacyNativeMyCombatGear(LegacyNativeGameClientRuntime _user)
    {
        user = _user;
        combatConstructorOwner = _user;
    }

    public LegacyNativeCombatGearRankTable.Entry GetEntry()
    {
        LegacyNativeCombatGearRankTable.Entry entry = new LegacyNativeCombatGearRankTable.Entry();
        entry.UID = user.Player.UID;
        entry.Name = user.Player.Name;
        entry.Level = (byte)user.Player.Level;
        entry.Class = user.Player.Class;
        entry.Mesh = user.Player.Mesh;
        entry.HairStyle = user.Player.Hair;
        entry.Garment = user.Player.GarmentId;
        if (user.Equipment != null)
        {
            entry.Head = user.Equipment.HeadID;
            entry.LeftWeapon = user.Equipment.LeftWeapon;
            entry.RightWeapon = user.Equipment.RightWeapon;
        }
        entry.RightWeaponAccessory = user.Player.RightWeaponAccessoryId;
        entry.LefttWeaponAccessory = user.Player.LeftWeaponAccessoryId;
        entry.MountArmor = user.Player.MountArmorId;
        entry.Wing = user.Player.WingId;
        if (user.Player.SpecialWingID != 0)
        {
            entry.Wing = user.Player.SpecialWingID;
        }
        entry.WingPlus = user.Player.WingPlus;
        entry.Title = user.Player.SpecialTitleID;
        entry.Armor = user.Player.ArmorId;
        if (user.Player.MyGuild != null)
        {
            entry.GuildName = user.Player.MyGuild.GuildName;
        }
        if (isOpen() != null)
        {
            entry.WeaponWar = (uint)isOpen().ItemID;
        }
        return entry;
    }

    public void UpdateRank()
    {
        UpdateRank(GetEntry());
    }

    public bool isOpen(TypeID type)
    {
        return isEquip(type, user.Player.Class);
    }

    public Weapon isOpen()
    {
        return isEquip(user.Player.Class);
    }
}
