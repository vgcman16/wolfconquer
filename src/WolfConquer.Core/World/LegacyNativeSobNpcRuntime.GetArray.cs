using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native SobNpc.GetArray: cached/live statue and both language branches.
internal partial class LegacyNativeSobNpcRuntime
{
	public unsafe LegacyNativePacket GetArray(LegacyNativePacket stream, bool view, bool st)
	{
		if (statue != null)
		{
			if (statue.StatuePacket != null && statue.Static)
			{
				stream.Seek(0);
				fixed (byte* statuePacket = statue.StatuePacket)
				{
					stream.memcpy(stream.Memory, statuePacket, statue.StatuePacket.Length);
				}
				stream.Size = statue.StatuePacket.Length;
				return stream;
			}
			LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto = new LegacyNativePlayerRuntime.SpawnPacketProto();
			spawnPacketProto.UID = statue.user!.Player.UID;
			spawnPacketProto.Mesh = statue.user!.Player.Mesh;
			spawnPacketProto.Head = statue.user!.Player.HeadId;
			spawnPacketProto.Garment = statue.user!.Player.GarmentId;
			spawnPacketProto.LeftWeapon = statue.user!.Player.LeftWeaponId;
			spawnPacketProto.LeftWeaponSoul = statue.user!.Player.LeftWeapsonSoul;
			spawnPacketProto.RightWeapon = statue.user!.Player.RightWeaponId;
			spawnPacketProto.RightWeaponSoul = statue.user!.Player.RightWeapsonSoul;
			spawnPacketProto.LeftWeaponAccessory = statue.user!.Player.LeftWeaponAccessoryId;
			spawnPacketProto.RightWeaponAccessory = statue.user!.Player.RightWeaponAccessoryId;
			spawnPacketProto.Steed = statue.user!.Player.SteedId;
			spawnPacketProto.MountArmor = statue.user!.Player.MountArmorId;
			spawnPacketProto.AppearanceType = 0;
			spawnPacketProto.GuildRank = (uint)statue.user!.Player.GuildRank;
			spawnPacketProto.GuildID = statue.user!.Player.GuildID;
			spawnPacketProto.Wing = statue.user!.Player.WingId;
			spawnPacketProto.WingPlus = statue.user!.Player.WingPlus;
			spawnPacketProto.WingProgress = statue.user!.Player.WingProgress;
			spawnPacketProto.Bottle = statue.user!.Player.Bottle;
			spawnPacketProto.Hitpoints = (uint)(HitPoints / 500);
			spawnPacketProto.X = X;
			spawnPacketProto.Y = Y;
			spawnPacketProto.HairStyle = statue.user!.Player.Hair;
			spawnPacketProto.Facing = (byte)statue.user!.Player.Angle;
			spawnPacketProto.Action = (ushort)(statue.Static ? 250 : ((ushort)statue.Action));
			spawnPacketProto.Reborn = statue.user!.Player.Reborn;
			spawnPacketProto.SecondRebornClass = statue.user!.Player.SecoundeClass;
			spawnPacketProto.FirstRebornClass = statue.user!.Player.FirstClass;
			spawnPacketProto.Level = statue.user!.Player.Level;
			spawnPacketProto.Away = statue.user!.Player.Away > 0;
			spawnPacketProto.ExtraBattlePower = statue.user!.Player.ExtraBattlePower;
			spawnPacketProto.FlowerIcon = statue.user!.Player.FlowerRank + 10000;
			spawnPacketProto.NobilityRank = (byte)statue.user!.Player.NobilityRank;
			spawnPacketProto.Armor = statue.user!.Player.ArmorId;
			spawnPacketProto.ArmorSoul = statue.user!.Player.ArmorSoul;
			spawnPacketProto.HeadSoul = statue.user!.Player.HeadSoul;
			spawnPacketProto.SteedColor = statue.user!.Player.SteedColor;
			spawnPacketProto.SteedPlus = statue.user!.Player.SteedPlus;
			spawnPacketProto.ClanUID = statue.user!.Player.ClanUID;
			spawnPacketProto.ClanRank = statue.user!.Player.ClanRank;
			spawnPacketProto.Title = statue.user!.Player.MyTitle;
			spawnPacketProto.ActiveSubClasses = (byte)statue.user!.Player.ActiveSublass;
			spawnPacketProto.SubClass = statue.user!.Player.SubClassHasPoints;
			spawnPacketProto.JiangHuActive = statue.user!.Player.JiangHuActive > 0;
			spawnPacketProto.JiangHuTalent = statue.user!.Player.JiangHuTalent;
			spawnPacketProto.MaxLife = statue.user!.Player.Owner.Status.MaxHitpoints;
			spawnPacketProto.ServerID = statue.user!.Player.ServerID;
			spawnPacketProto.UnionExploits = statue.user!.Player.KingDomExploits;
			spawnPacketProto.UnionID = (statue.user!.Player.InUnion ? statue.user!.Player.MyUnion!.UID : 0u);
			spawnPacketProto.UnionRank = (uint)(statue.user!.Player.InUnion ? LegacyNativeUnionMember.GetRank(statue.user!.Player.UnionMemeber!.Rank) : LegacyNativeUnionMember.Ranks.Member);
			spawnPacketProto.UnionType = (uint)(statue.user!.Player.InUnion ? statue.user!.Player.MyUnion!.IsKingdom : 0);
			spawnPacketProto.OwnerUID = statue.user!.Player.RealUID;
			spawnPacketProto.BattlePower = (int)(statue.user!.Player.BattlePower - statue.user!.Player.GuildBattlePower);
			spawnPacketProto.QuizPoints = statue.user!.Player.QuizPoints;
			spawnPacketProto.Class = statue.user!.Player.Class;
			spawnPacketProto.GuildBattlePower = statue.user!.Player.GuildBattlePower;
			spawnPacketProto.MyWing = statue.user!.Player.SpecialWingID;
			spawnPacketProto.MyTitle = statue.user!.Player.SpecialTitleID;
			spawnPacketProto.MyTitleScore = statue.user!.Player.SpecialTitleScore;
			spawnPacketProto.CountryCode = statue.user!.Player.CountryID;
			spawnPacketProto.MainFlag = (byte)statue.user!.Player.MainFlag;
			spawnPacketProto.Official_Harem_Guards = (uint)(statue.user!.Player.InUnion ? statue.user!.Player.ExploitsRank : ((LegacyNativePlayerExploitsRank)0u));
			spawnPacketProto.Names = new string[3]
			{
				(statue.user!.Player.Language == "En") ? Name! : Name2!,
				string.Empty,
				statue.user!.Player.ClanName
			};
			LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto2 = spawnPacketProto;
			spawnPacketProto2.StatusFlags = new ulong[BitVector.bits.Length];
			stream.ProtoBufferSerialize(spawnPacketProto2);
			stream.Finalize(2227);
			if (statue.StatuePacket == null && statue.Static)
			{
				statue.StatuePacket = new byte[stream.Size];
				int size = stream.Size;
				fixed (byte* statuePacket2 = statue.StatuePacket)
				{
					stream.memcpy(statuePacket2, stream.Memory, size);
				}
			}
			return stream;
		}
		stream.InitWriter();
		string text = "";
		text = ((!st) ? "En" : "Ar");
		if (text == "Ar")
		{
			SobNpcSpawnProto obj = new SobNpcSpawnProto
			{
				UID = UID,
				Hitpoints = (uint)HitPoints,
				MaxHitpoints = (uint)MaxHitPoints,
				X = X,
				Y = Y,
				Mesh = (ushort)Mesh,
				Type = (byte)Type,
				Sort = Sort,
				Name = Name2
			};
			stream.ProtoBufferSerialize(obj);
			stream.Finalize(2166);
		}
		else
		{
			SobNpcSpawnProto obj2 = new SobNpcSpawnProto
			{
				UID = UID,
				Hitpoints = (uint)HitPoints,
				MaxHitpoints = (uint)MaxHitPoints,
				X = X,
				Y = Y,
				Mesh = (ushort)Mesh,
				Type = (byte)Type,
				Sort = Sort,
				Name = Name
			};
			stream.ProtoBufferSerialize(obj2);
			stream.Finalize(2166);
		}
		return stream;
	}
}
