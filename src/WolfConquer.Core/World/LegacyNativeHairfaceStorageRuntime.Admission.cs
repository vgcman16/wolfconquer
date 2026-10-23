using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
public bool Add(LegacyNativeHairfaceStorageTable.Hairface? item, bool Send = true)
	{
		if (item == null)
		{
			return false;
		}
		if (!Exists(item.ID, item.Type))
		{
			items.Add(item);
			objects = items.ToArray();
			if (Send)
			{
				using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
				LegacyNativePacket stream = recycledPacket.GetStream();
				Owner!.Send(stream.CreateHairface(new LegacyNativeMsgHairfaceStorage.MsgHairfaceStorageProto
				{
					Type = LegacyHairFaceStorageAction.Add,
					Items = new LegacyNativeMsgHairfaceStorage.Item[1]
					{
						new LegacyNativeMsgHairfaceStorage.Item
						{
							Type = item.Type,
							ID = item.ID,
							Colors = new long[2] { 0L, 1L }
						}
					}
				}));
			}
			return true;
		}
		return false;
	}

public bool AddColor(LegacyNativeHairfaceStorageTable.HairColor item)
	{
		if (Exists(item.ID, LegacyHairFaceStorageType.Hairstyle))
		{
			items.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == item.ID && i.Type == LegacyHairFaceStorageType.Hairstyle).FirstOrDefault()!.Colors![item.Color] = 1;
			objects = items.ToArray();
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				Owner!.Send(stream.CreateHairface(new LegacyNativeMsgHairfaceStorage.MsgHairfaceStorageProto
				{
					Type = LegacyHairFaceStorageAction.AddColor,
					HairColor = new LegacyNativeMsgHairfaceStorage.Operation
					{
						ID = item.ID,
						dwParam = item.Color
					}
				}));
			}
			return true;
		}
		return false;
	}
}
