using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
public bool CanEquip(LegacyNativeHairfaceStorageTable.Hairface hair)
	{
		return (hair.Sex == 0 || (LegacyNativeRoleCore.IsBoy(Owner!.Player.Body) && hair.Sex == 1) || (LegacyNativeRoleCore.IsGirl(Owner!.Player.Body) && hair.Sex == 2)) && Owner!.Player.VipLevel >= hair.RequiredVIPLevel && (hair.Classes!.Count == 0 || hair.Classes!.Contains((byte)(Owner!.Player.Class / 1000)));
	}

public bool Equip(LegacyNativeHairfaceStorageTable.Hairface hair)
	{
		if (Exists(hair.ID, hair.Type) && CanEquip(hair))
		{
			if (hair.Type == LegacyHairFaceStorageType.Hairstyle)
			{
				Owner!.Player.Hair = (ushort)hair.ID;
				Owner!.Player.HairColor = hair.EquippedColor;
			}
			else
			{
				Owner!.Player.Face = (ushort)hair.ID;
			}
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				Owner!.Send(stream.CreateHairface(new LegacyNativeMsgHairfaceStorage.MsgHairfaceStorageProto
				{
					Type = LegacyHairFaceStorageAction.Equip,
					Hair = new LegacyNativeMsgHairfaceStorage.Operation
					{
						ID = (uint)hair.Type,
						dwParam = hair.ID
					}
				}));
			}
			return true;
		}
		return false;
	}

public bool EquipColor(uint ID, byte Color)
	{
		if (Exists(ID, LegacyHairFaceStorageType.Hairstyle) && items.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == ID && i.Type == LegacyHairFaceStorageType.Hairstyle).FirstOrDefault()!.Colors![Color] == 1 && Owner!.Player.Hair % 1000 == ID)
		{
			items.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == ID && i.Type == LegacyHairFaceStorageType.Hairstyle).FirstOrDefault()!.EquippedColor = Color;
			objects = items.ToArray();
			Owner!.Player.HairColor = Color;
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				Owner!.Send(stream.CreateHairface(new LegacyNativeMsgHairfaceStorage.MsgHairfaceStorageProto
				{
					Type = LegacyHairFaceStorageAction.EquipColor,
					HairColor = new LegacyNativeMsgHairfaceStorage.Operation
					{
						ID = ID,
						dwParam = Color
					}
				}));
			}
			return true;
		}
		return false;
	}
}
