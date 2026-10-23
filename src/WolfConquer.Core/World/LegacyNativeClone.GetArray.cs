using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClone
{
	public LegacyNativePacket GetArray(LegacyNativePacket stream)
	{
		stream.InitWriter();
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto = new LegacyNativePlayerRuntime.SpawnPacketProto();
		spawnPacketProto.UID = UID;
		spawnPacketProto.Mesh = Owner.Player.Mesh;
		spawnPacketProto.Head = Owner.Player.HeadId;
		spawnPacketProto.Garment = Owner.Player.GarmentId;
		spawnPacketProto.LeftWeapon = Owner.Player.LeftWeaponId;
		spawnPacketProto.LeftWeaponSoul = Owner.Player.LeftWeapsonSoul;
		spawnPacketProto.RightWeapon = Owner.Player.RightWeaponId;
		spawnPacketProto.RightWeaponSoul = Owner.Player.RightWeapsonSoul;
		spawnPacketProto.LeftWeaponAccessory = Owner.Player.LeftWeaponAccessoryId;
		spawnPacketProto.RightWeaponAccessory = Owner.Player.RightWeaponAccessoryId;
		spawnPacketProto.Steed = Owner.Player.SteedId;
		spawnPacketProto.MountArmor = Owner.Player.MountArmorId;
		spawnPacketProto.AppearanceType = (ushort)Owner.Player.AparenceType;
		spawnPacketProto.GuildRank = (uint)Owner.Player.GuildRank;
		spawnPacketProto.GuildID = Owner.Player.GuildID;
		spawnPacketProto.Wing = Owner.Player.WingId;
		spawnPacketProto.WingPlus = Owner.Player.WingPlus;
		spawnPacketProto.WingProgress = Owner.Player.WingProgress;
		spawnPacketProto.Bottle = Owner.Player.Bottle;
		spawnPacketProto.Hitpoints = (uint)Owner.Player.HitPoints;
		spawnPacketProto.X = Owner.Player.X;
		spawnPacketProto.Y = Owner.Player.Y;
		spawnPacketProto.HairStyle = Owner.Player.Hair;
		spawnPacketProto.Facing = (byte)Owner.Player.Angle;
		spawnPacketProto.Action = (ushort)Owner.Player.Action;
		spawnPacketProto.Reborn = Owner.Player.Reborn;
		spawnPacketProto.SecondRebornClass = Owner.Player.SecoundeClass;
		spawnPacketProto.FirstRebornClass = Owner.Player.FirstClass;
		spawnPacketProto.Level = Owner.Player.Level;
		spawnPacketProto.ExtraBattlePower = Owner.Player.ExtraBattlePower;
		spawnPacketProto.FlowerIcon = Owner.Player.FlowerRank + 10000;
		spawnPacketProto.NobilityRank = (byte)Owner.Player.NobilityRank;
		spawnPacketProto.Armor = Owner.Player.ArmorId;
		spawnPacketProto.ArmorSoul = Owner.Player.ArmorSoul;
		spawnPacketProto.HeadSoul = Owner.Player.HeadSoul;
		spawnPacketProto.SteedColor = Owner.Player.SteedColor;
		spawnPacketProto.SteedPlus = Owner.Player.SteedPlus;
		spawnPacketProto.ClanUID = Owner.Player.ClanUID;
		spawnPacketProto.ClanRank = Owner.Player.ClanRank;
		spawnPacketProto.Title = Owner.Player.MyTitle;
		spawnPacketProto.ActiveSubClasses = (byte)Owner.Player.ActiveSublass;
		spawnPacketProto.SubClass = Owner.Player.SubClassHasPoints;
		spawnPacketProto.JiangHuActive = Owner.Player.JiangHuActive > 0;
		spawnPacketProto.JiangHuTalent = Owner.Player.JiangHuTalent;
		spawnPacketProto.MaxLife = Owner.Status.MaxHitpoints;
		spawnPacketProto.OwnerUID = Owner.Player.UID;
		spawnPacketProto.BattlePower = Owner.Player.BattlePower;
		spawnPacketProto.Class = Owner.Player.Class;
		spawnPacketProto.MainFlag = (byte)Owner.Player.MainFlag;
		spawnPacketProto.OwnerPet = 2u;
		spawnPacketProto.OwnerPet1 = (ushort)cloneflag;
		spawnPacketProto.Names = new string[5]
		{
			Owner.Player.Name,
			string.Empty,
			string.Empty,
			string.Empty,
			"ShadowClone(" + Owner.Player.Name + ")"
		};
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto2 = spawnPacketProto;
		spawnPacketProto2.StatusFlags = new ulong[BitVector.bits.Length];
		stream.ProtoBufferSerialize(spawnPacketProto2);
		stream.Finalize(2227);
		return stream;
	}
}
