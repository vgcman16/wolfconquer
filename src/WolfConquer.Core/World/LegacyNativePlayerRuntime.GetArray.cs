using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player.GetArray4818..5066 layout; both branches resolve supported prestige classes.
	internal LegacyNativePacket GetArray(LegacyNativePacket stream, bool WindowsView, bool st)
	{
		SpawnPacketProto spawnPacketProto;
		if (Owner.Player.SpawinEffect)
		{
			stream.InitWriter();
			spawnPacketProto = new SpawnPacketProto();
			spawnPacketProto.UID = UID;
			spawnPacketProto.Mesh = Mesh;
			spawnPacketProto.Head = HeadId;
			spawnPacketProto.Garment = GarmentId;
			spawnPacketProto.LeftWeapon = LeftWeaponId;
			spawnPacketProto.LeftWeaponSoul = LeftWeapsonSoul;
			spawnPacketProto.RightWeapon = RightWeaponId;
			spawnPacketProto.RightWeaponSoul = RightWeapsonSoul;
			spawnPacketProto.LeftWeaponAccessory = LeftWeaponAccessoryId;
			spawnPacketProto.RightWeaponAccessory = RightWeaponAccessoryId;
			spawnPacketProto.Steed = SteedId;
			spawnPacketProto.MountArmor = MountArmorId;
			spawnPacketProto.AppearanceType = (ushort)AparenceType;
			spawnPacketProto.GuildRank = (uint)GuildRank;
			spawnPacketProto.GuildID = GuildID;
			spawnPacketProto.Wing = 0u;
			spawnPacketProto.WingPlus = 0u;
			spawnPacketProto.WingProgress = 0u;
			spawnPacketProto.Bottle = Bottle;
			spawnPacketProto.Hitpoints = (uint)HitPoints;
			spawnPacketProto.X = X;
			spawnPacketProto.Y = Y;
			spawnPacketProto.HairStyle = Hair;
			spawnPacketProto.Facing = (byte)Angle;
			spawnPacketProto.Action = (ushort)Action;
			spawnPacketProto.Reborn = Reborn;
			spawnPacketProto.SecondRebornClass = SecoundeClass;
			spawnPacketProto.FirstRebornClass = FirstClass;
			spawnPacketProto.Level = Level;
			spawnPacketProto.WindowSpawn = WindowsView;
			spawnPacketProto.Away = Away > 0;
			spawnPacketProto.ExtraBattlePower = ExtraBattlePower - GuildBattlePower;
			spawnPacketProto.FlowerIcon = FlowerRank + 10000;
			spawnPacketProto.NobilityRank = (byte)NobilityRank;
			spawnPacketProto.Armor = ArmorId;
			spawnPacketProto.ArmorSoul = ArmorSoul;
			spawnPacketProto.HeadSoul = HeadSoul;
			spawnPacketProto.SteedColor = SteedColor;
			spawnPacketProto.SteedPlus = SteedPlus;
			spawnPacketProto.ClanUID = ClanUID;
			spawnPacketProto.ClanRank = ClanRank;
			spawnPacketProto.Title = 0u;
			spawnPacketProto.ActiveSubClasses = (byte)ActiveSublass;
			spawnPacketProto.SubClass = SubClassHasPoints;
			spawnPacketProto.JiangHuActive = JiangHuActive > 0;
			spawnPacketProto.JiangHuTalent = JiangHuTalent;
			spawnPacketProto.MaxLife = Owner.Status.MaxHitpoints;
			spawnPacketProto.ServerID = ServerID;
			spawnPacketProto.UnionExploits = KingDomExploits;
			spawnPacketProto.UnionID = (InUnion ? MyUnion!.UID : 0u);
			spawnPacketProto.UnionRank = (uint)(InUnion ? LegacyNativeUnionMember.GetRank(UnionMemeber!.Rank) : LegacyNativeUnionMember.Ranks.Member);
			spawnPacketProto.UnionType = (uint)(InUnion ? MyUnion!.IsKingdom : 0);
			spawnPacketProto.OwnerUID = RealUID;
			spawnPacketProto.BattlePower = BattlePower;
			spawnPacketProto.QuizPoints = QuizPoints;
			spawnPacketProto.Class = Class;
			spawnPacketProto.GuildBattlePower = GuildBattlePower;
			spawnPacketProto.MyWing = 0u;
			spawnPacketProto.MyTitle = 0u;
			spawnPacketProto.MyTitleScore = 0u;
			spawnPacketProto.CountryCode = CountryID;
			spawnPacketProto.PokerSeat = (byte)((Owner.PokerPlayer != null) ? Owner.PokerPlayer.Seat : 0);
			spawnPacketProto.PokerTableID = ((Owner.PokerPlayer != null && Owner.PokerPlayer.Table != null) ? Owner.PokerPlayer.Table.Id : 0u);
			spawnPacketProto.InnerStrengthScore = InnerPower!.TotalScore;
			spawnPacketProto.MainFlag = 0;
			spawnPacketProto.Enlighten = Enilghten;
			spawnPacketProto.Official_Harem_Guards = (uint)(InUnion ? ExploitsRank : ((LegacyNativePlayerExploitsRank)0u));
			spawnPacketProto.Names = new string[7]
			{
				Name,
				"",
				ClanName,
				"",
				"",
				(MyGuild != null) ? MyGuild.GuildName : string.Empty,
				(!InUnion) ? string.Empty : MyUnion!.Name
			};
			SpawnPacketProto spawnPacketProto2 = spawnPacketProto;
			int myRank = LegacyNativePrestigeRanking.TryGetIndex(Class, out var prestigeCategory)
				? (int)LegacyNativePrestigeRanking.GetMyRank(prestigeCategory, UID) : 0;
			if (myRank >= 1 && myRank <= 3)
			{
				spawnPacketProto2.PrestigeRank = (byte)myRank;
			}
			spawnPacketProto2.UnknownBool = true;
			if (Owner.Equipment!.TryGetEquip(Flags.ConquerItem.Relics, out var itemdata))
			{
				spawnPacketProto2.Relic = (byte)(itemdata.RelicAttributes.Where((LegacyNativeRelicAttribute i) => (uint)i != 0 && i.Epic).Count() * 10 + 1);
			}
			spawnPacketProto2.StatusFlags = new ulong[BitVector.bits.Length];
			for (int num = 0; num < BitVector.bits.Length; num++)
			{
				spawnPacketProto2.StatusFlags[num] = BitVector.bits[num];
			}
			spawnPacketProto2.HundredWeapons = new uint[5];
			LegacyNativeHundredWeaponInfo[] array = (from i in Owner.HundredWeapons!.Objects.Values
				where i.AppearancePosition > 0
				orderby i.AppearancePosition
				select i).ToArray();
			for (int num2 = 0; num2 < Math.Min(spawnPacketProto2.HundredWeapons.Length, array.Length); num2++)
			{
				string text = Convert.ToString((int)array[num2].WeaponSubtype, 2) + Convert.ToString(array[num2].Score, 2);
				while (text.Length < 25)
				{
					text += "0";
				}
				spawnPacketProto2.HundredWeapons[num2] = Convert.ToUInt32(text, 2);
			}
			uint[] effect = Owner.CombatGear!.GetEffect(Class);
			if (effect != null)
			{
				spawnPacketProto2.UnknownBool = true;
				spawnPacketProto2.HundredWeapons = effect.ToArray();
			}
			spawnPacketProto2.CollectionID = CollectionID;
			spawnPacketProto2.Test1 = 1u;
			spawnPacketProto2.Test2 = 1u;
			stream.ProtoBufferSerialize(spawnPacketProto2);
			stream.Finalize(2227);
			return stream;
		}
		stream.InitWriter();
		spawnPacketProto = new SpawnPacketProto();
		spawnPacketProto.UID = UID;
		spawnPacketProto.Mesh = Mesh;
		spawnPacketProto.Head = HeadId;
		spawnPacketProto.Garment = GarmentId;
		spawnPacketProto.LeftWeapon = LeftWeaponId;
		spawnPacketProto.LeftWeaponSoul = LeftWeapsonSoul;
		spawnPacketProto.RightWeapon = RightWeaponId;
		spawnPacketProto.RightWeaponSoul = RightWeapsonSoul;
		spawnPacketProto.LeftWeaponAccessory = LeftWeaponAccessoryId;
		spawnPacketProto.RightWeaponAccessory = RightWeaponAccessoryId;
		spawnPacketProto.Steed = SteedId;
		spawnPacketProto.MountArmor = MountArmorId;
		spawnPacketProto.AppearanceType = (ushort)AparenceType;
		spawnPacketProto.GuildRank = (uint)GuildRank;
		spawnPacketProto.GuildID = GuildID;
		spawnPacketProto.Wing = WingId;
		spawnPacketProto.WingPlus = WingPlus;
		spawnPacketProto.WingProgress = WingProgress;
		spawnPacketProto.Bottle = Bottle;
		spawnPacketProto.Hitpoints = (uint)HitPoints;
		spawnPacketProto.X = X;
		spawnPacketProto.Y = Y;
		spawnPacketProto.HairStyle = Hair;
		spawnPacketProto.Facing = (byte)Angle;
		spawnPacketProto.Action = (ushort)Action;
		spawnPacketProto.Reborn = Reborn;
		spawnPacketProto.SecondRebornClass = SecoundeClass;
		spawnPacketProto.FirstRebornClass = FirstClass;
		spawnPacketProto.Level = Level;
		spawnPacketProto.WindowSpawn = WindowsView;
		spawnPacketProto.Away = Away > 0;
		spawnPacketProto.ExtraBattlePower = ExtraBattlePower - GuildBattlePower;
		spawnPacketProto.FlowerIcon = FlowerRank + 10000;
		spawnPacketProto.NobilityRank = (byte)NobilityRank;
		spawnPacketProto.Armor = ArmorId;
		spawnPacketProto.ArmorSoul = ArmorSoul;
		spawnPacketProto.HeadSoul = HeadSoul;
		spawnPacketProto.SteedColor = SteedColor;
		spawnPacketProto.SteedPlus = SteedPlus;
		spawnPacketProto.ClanUID = ClanUID;
		spawnPacketProto.ClanRank = ClanRank;
		spawnPacketProto.Title = MyTitle;
		spawnPacketProto.ActiveSubClasses = (byte)ActiveSublass;
		spawnPacketProto.SubClass = SubClassHasPoints;
		spawnPacketProto.JiangHuActive = JiangHuActive > 0;
		spawnPacketProto.JiangHuTalent = JiangHuTalent;
		spawnPacketProto.MaxLife = Owner.Status.MaxHitpoints;
		spawnPacketProto.ServerID = ServerID;
		spawnPacketProto.UnionExploits = KingDomExploits;
		spawnPacketProto.UnionID = (InUnion ? MyUnion!.UID : 0u);
		spawnPacketProto.UnionRank = (uint)(InUnion ? LegacyNativeUnionMember.GetRank(UnionMemeber!.Rank) : LegacyNativeUnionMember.Ranks.Member);
		spawnPacketProto.UnionType = (uint)(InUnion ? MyUnion!.IsKingdom : 0);
		spawnPacketProto.OwnerUID = RealUID;
		spawnPacketProto.BattlePower = BattlePower;
		spawnPacketProto.QuizPoints = QuizPoints;
		spawnPacketProto.Class = Class;
		spawnPacketProto.GuildBattlePower = GuildBattlePower;
		spawnPacketProto.MyWing = SpecialWingID;
		spawnPacketProto.MyTitle = SpecialTitleID;
		spawnPacketProto.MyTitleScore = SpecialTitleScore;
		spawnPacketProto.CountryCode = CountryID;
		spawnPacketProto.PokerSeat = (byte)((Owner.PokerPlayer != null) ? Owner.PokerPlayer.Seat : 0);
		spawnPacketProto.PokerTableID = ((Owner.PokerPlayer != null && Owner.PokerPlayer.Table != null) ? Owner.PokerPlayer.Table.Id : 0u);
		spawnPacketProto.InnerStrengthScore = InnerPower!.TotalScore;
		spawnPacketProto.MainFlag = (byte)MainFlag;
		spawnPacketProto.Enlighten = Enilghten;
		spawnPacketProto.Official_Harem_Guards = (uint)(InUnion ? ExploitsRank : ((LegacyNativePlayerExploitsRank)0u));
		spawnPacketProto.Names = new string[7]
		{
			Name,
			"",
			ClanName,
			"",
			"",
			(MyGuild != null) ? MyGuild.GuildName : string.Empty,
			(!InUnion) ? string.Empty : MyUnion!.Name
		};
		SpawnPacketProto spawnPacketProto3 = spawnPacketProto;
		int myRank2 = LegacyNativePrestigeRanking.TryGetIndex(Class, out var prestigeCategory2)
			? (int)LegacyNativePrestigeRanking.GetMyRank(prestigeCategory2, UID) : 0;
		if (myRank2 >= 1 && myRank2 <= 3)
		{
			spawnPacketProto3.PrestigeRank = (byte)myRank2;
		}
		spawnPacketProto3.UnknownBool = true;
		if (Owner.Equipment!.TryGetEquip(Flags.ConquerItem.Relics, out var itemdata2))
		{
			spawnPacketProto3.Relic = (byte)(itemdata2.RelicAttributes.Where((LegacyNativeRelicAttribute i) => (uint)i != 0 && i.Epic).Count() * 10 + 1);
		}
		spawnPacketProto3.StatusFlags = new ulong[BitVector.bits.Length];
		for (int num3 = 0; num3 < BitVector.bits.Length; num3++)
		{
			spawnPacketProto3.StatusFlags[num3] = BitVector.bits[num3];
		}
		spawnPacketProto3.HundredWeapons = new uint[5];
		LegacyNativeHundredWeaponInfo[] array2 = (from i in Owner.HundredWeapons!.Objects.Values
			where i.AppearancePosition > 0
			orderby i.AppearancePosition
			select i).ToArray();
		for (int num4 = 0; num4 < Math.Min(spawnPacketProto3.HundredWeapons.Length, array2.Length); num4++)
		{
			string text2 = Convert.ToString((int)array2[num4].WeaponSubtype, 2) + Convert.ToString(array2[num4].Score, 2);
			while (text2.Length < 25)
			{
				text2 += "0";
			}
			spawnPacketProto3.HundredWeapons[num4] = Convert.ToUInt32(text2, 2);
		}
		uint[] effect2 = Owner.CombatGear!.GetEffect(Class);
		if (effect2 != null)
		{
			spawnPacketProto3.UnknownBool = true;
			spawnPacketProto3.HundredWeapons = effect2.ToArray();
		}
		spawnPacketProto3.Test1 = 1u;
		spawnPacketProto3.Test2 = 1u;
		spawnPacketProto3.CollectionID = CollectionID;
		stream.ProtoBufferSerialize(spawnPacketProto3);
		stream.Finalize(2227);
		return stream;
	}

}
