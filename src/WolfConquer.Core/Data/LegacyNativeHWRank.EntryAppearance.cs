using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeHWRank
{
    public partial class Entry
    {
        public void AddInfo(LegacyNativeGameClientRuntime user)
        {
            HairStyle = user.Player.Hair;
            Garment = user.Player.GarmentId;
            if (user.Equipment != null)
            {
                Head = user.Equipment.HeadID;
                LeftWeapon = user.Equipment.LeftWeapon;
                RightWeapon = user.Equipment.RightWeapon;
            }
            RightWeaponAccessory = user.Player.RightWeaponAccessoryId;
            LefttWeaponAccessory = user.Player.LeftWeaponAccessoryId;
            MountArmor = user.Player.MountArmorId;
            Wing = user.Player.WingId;
            if (user.Player.SpecialWingID != 0)
            {
                Wing = user.Player.SpecialWingID;
            }
            WingPlus = user.Player.WingPlus;
            Title = user.Player.SpecialTitleID;
            Armor = user.Player.ArmorId;
            if (user.Player.MyGuild != null)
            {
                GuildName = user.Player.MyGuild.GuildName;
            }
            if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.WeeklyPKChampion))
            {
                Flag = 37u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopTrojan))
            {
                Flag = 39u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopWarrior))
            {
                Flag = 38u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopArcher))
            {
                Flag = 40u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopNinja))
            {
                Flag = 43u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopMonk))
            {
                Flag = 114u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopPirate))
            {
                Flag = 122u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopDragonLee))
            {
                Flag = 154u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopFireTaoist))
            {
                Flag = 42u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopWaterTaoist))
            {
                Flag = 41u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopDeputyLeader))
            {
                Flag = 35u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopGuildLeader))
            {
                Flag = 34u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopWindWalker))
            {
                Flag = 203u;
            }
            else if (user.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.TopThunderstriker))
            {
                Flag = 232u;
            }
        }
    }
}
