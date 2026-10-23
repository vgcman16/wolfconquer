using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterRole
{
	public LegacyNativePacket GetArray(LegacyNativePacket stream, bool view, bool st)
	{
		if (IsFloor && Mesh != 980)
		{
			return stream.ItemPacketCreate(FloorPacket!);
		}
		stream.InitWriter();
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto = new LegacyNativePlayerRuntime.SpawnPacketProto();
		spawnPacketProto.UID = UID;
		spawnPacketProto.Mesh = Mesh;
		spawnPacketProto.MonsterLevel = Level;
		spawnPacketProto.Hitpoints = (IsFloor ? ((uint)StampFloorSecounds) : HitPoints);
		spawnPacketProto.X = X;
		spawnPacketProto.Y = Y;
		spawnPacketProto.Action = (ushort)Action;
		spawnPacketProto.Level = Level;
		spawnPacketProto.Names = new string[4]
		{
			Name!,
			string.Empty,
			string.Empty,
			string.Empty
		};
		spawnPacketProto.MonsterID = Family.ID;
		LegacyNativePlayerRuntime.SpawnPacketProto spawnPacketProto2 = spawnPacketProto;
		if (Family.Name == "NemesisTyrant")
		{
			spawnPacketProto2.MonsterID = 4220u;
		}
		if (Family.Name == "BloodyBanshee")
		{
			spawnPacketProto2.MonsterID = 3976u;
		}
		if (Family.Name == "DragonWraith")
		{
			spawnPacketProto2.MonsterID = 3971u;
		}
		if (Family.Name == "QueenofEvil")
		{
			spawnPacketProto2.MonsterID = 3970u;
		}
		if (Family.Name == "RockMonsterKing")
		{
			spawnPacketProto2.MonsterID = 8311u;
		}
		if (Family.Name == "NetherTyrant")
		{
			spawnPacketProto2.MonsterID = 3978u;
		}
		if (Family.Name == "ThrillingSpook")
		{
			spawnPacketProto2.MonsterID = 4212u;
		}
		if (Family.Name == "SnowBanshee")
		{
			spawnPacketProto2.MonsterID = 4171u;
		}
		if (Family.Name == "FrostSerpent")
		{
			spawnPacketProto2.MonsterID = 2414u;
		}
		if (Family.Name == "ChillingSpook")
		{
			spawnPacketProto2.MonsterID = 3977u;
		}
		if (Family.Name == "BladeDevilLord")
		{
			spawnPacketProto2.MonsterID = 2417u;
		}
		if (Family.Name == "BladeDevilKing")
		{
			spawnPacketProto2.MonsterID = 2418u;
		}
		if (Family.Name == "BladeDevilEnvoy")
		{
			spawnPacketProto2.MonsterID = 2415u;
		}
		if (Family.Name == "SerpentLord")
		{
			spawnPacketProto2.MonsterID = 2412u;
		}
		if (Family.Name == "NightmareCaptain")
		{
			spawnPacketProto2.MonsterID = 7563u;
		}
		if (Family.Name == "RedDevilL118")
		{
			spawnPacketProto2.MonsterID = 4858u;
		}
		if (Family.Name == "MonkeyKing")
		{
			spawnPacketProto2.MonsterID = 8305u;
		}
		if (Family.Name == "MonkeyMessenger")
		{
			spawnPacketProto2.MonsterID = 8105u;
		}
		if (Family.Name == "MonkeyAide")
		{
			spawnPacketProto2.MonsterID = 8205u;
		}
		if (Family.ID == 20010)
		{
			spawnPacketProto2.MonsterID = 2416u;
		}
		if (Family.ID == 20000)
		{
			spawnPacketProto2.MonsterID = 4860u;
		}
		spawnPacketProto2.Boss = Boss > 0;
		if (Mesh == 980)
		{
			spawnPacketProto2.OwnerPet = 3u;
			spawnPacketProto2.Facing = (byte)OwnerFloor!.Player.Angle;
			spawnPacketProto2.OwnerUID = OwnerFloor!.Player.UID;
			spawnPacketProto2.GuildID = OwnerFloor!.Player.GuildID;
			spawnPacketProto2.OwnerPet1 = 15u;
			spawnPacketProto2.MonsterID = 4264u;
		}
		if (Mesh == 138 || Mesh == 137 || Mesh == 848)
		{
			spawnPacketProto2.OwnerPet = PetFlag;
			spawnPacketProto2.Facing = (byte)OwnerFloor!.Player.Angle;
			spawnPacketProto2.OwnerUID = OwnerFloor!.Player.UID;
			spawnPacketProto2.OwnerPet1 = 15u;
			spawnPacketProto2.MonsterID = Family.ID;
		}
		spawnPacketProto2.MaxLife = (uint)Family.MaxHealth;
		spawnPacketProto2.StatusFlags = new ulong[BitVector.bits.Length];
		for (int i = 0; i < BitVector.bits.Length; i++)
		{
			spawnPacketProto2.StatusFlags[i] = BitVector.bits[i];
		}
		if (!IsFloor && Boss > 0)
		{
			uint num = (uint)(Family.MaxHealth / 10000);
			if (num != 0)
			{
				spawnPacketProto2.Hitpoints /= num;
			}
			else
			{
				spawnPacketProto2.Hitpoints = (uint)(spawnPacketProto2.Hitpoints * Family.MaxHealth);
			}
		}
		stream.ProtoBufferSerialize(spawnPacketProto2);
		stream.Finalize(2227);
		return stream;
	}
}
