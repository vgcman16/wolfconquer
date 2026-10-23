using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public partial class Entry
	{
		public void AddInfo(LegacyNativeGameClientRuntime user)
		{
			uint? preparedFlag = LegacyNativeRankAppearanceFlags.Read(user.Player, ninjaPriorities: false);
			HairStyle = user.Player.Hair;
			Head = user.Equipment!.HeadID;
			Garment = user.Player.GarmentId;
			LeftWeapon = user.Equipment!.LeftWeapon;
			RightWeapon = user.Equipment!.RightWeapon;
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
			if (preparedFlag.HasValue) Flag = preparedFlag.Value;
		}

		public void UpdateInfo(LegacyNativeGameClientRuntime user)
		{
			HairStyle = user.Player.Hair;
			Head = user.Equipment!.HeadID;
			Garment = user.Player.GarmentId;
			LeftWeapon = user.Equipment!.LeftWeapon;
			RightWeapon = user.Equipment!.RightWeapon;
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
		}
	}

}
