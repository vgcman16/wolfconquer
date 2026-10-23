using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
	public partial class Entry
	{
		public static implicit operator LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto.Entry(Entry BestOf)
		{
			return new LegacyNativeMsgRankMemberShow.MsgRankMemberShowProto.Entry
			{
				EntityUID = BestOf.UID,
				Flag = BestOf.Flag,
				Garment = BestOf.Garment,
				GuildName = BestOf.GuildName,
				HairStyle = BestOf.HairStyle,
				Head = BestOf.Head,
				LefttWeaponAccessory = BestOf.LefttWeaponAccessory,
				LeftWeapon = BestOf.LeftWeapon,
				Mesh = BestOf.Mesh,
				MountArmor = BestOf.MountArmor,
				Name = BestOf.Name,
				Rank = 1u,
				RightWeapon = BestOf.RightWeapon,
				RightWeaponAccessory = BestOf.RightWeaponAccessory,
				Title = BestOf.Title,
				Type = 1u,
				Armor = BestOf.Armor,
				Wing = BestOf.Wing,
				WingPlus = BestOf.WingPlus
			};
		}
	}

}
