using WolfConquer.Core.Networking;
using Actions = WolfConquer.Core.World.LegacyHairFaceStorageAction;
using HairfaceType = WolfConquer.Core.World.LegacyHairFaceStorageType;

namespace WolfConquer.Core.World;

// Whole source method; private receiver is deliberately unregistered, no Packet2282 adoption.
internal static partial class LegacyNativeMsgHairfaceStorage
{
private static void Process(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
	{
		stream.GetHairface(out var Info);
		switch (Info.Type)
		{
		case Actions.Login:
		{
			Info.Items = new Item[client.HairfaceStorage!.Objects.Count()];
			for (int num = 0; num < Info.Items.Length; num++)
			{
				Info.Items[num] = new Item();
				Info.Items[num].ID = client.HairfaceStorage!.Objects.ToArray()[num].ID;
				Info.Items[num].Type = client.HairfaceStorage!.Objects.ToArray()[num].Type;
				Info.Items[num].Equipped = (client.HairfaceStorage!.Objects.ToArray()[num].ID == client.Player.Hair % 1000 && LegacyNativeHairfaceStorageTable.Hairfaces.ToArray()[num].Type == HairfaceType.Hairstyle) || (client.Player.Face == client.HairfaceStorage!.Objects.ToArray()[num].ID && LegacyNativeHairfaceStorageTable.Hairfaces.ToArray()[num].Type == HairfaceType.Avatar);
				if (Info.Items[num].Type == HairfaceType.Hairstyle)
				{
					Info.Items[num].Colors = new long[2];
					Info.Items[num].Colors![0] = client.HairfaceStorage!.Objects.ToArray()[num].EquippedColor;
					string text = "";
					for (byte b = 0; b < client.HairfaceStorage!.Objects.ToArray()[num].Colors!.Length; b++)
					{
						text += Convert.ToString(client.HairfaceStorage!.Objects.ToArray()[num].Colors![b]);
					}
					Info.Items[num].Colors![1] = Convert.ToInt64(LegacyNativeBaseFunc.ReverseString(text), 2);
				}
			}
			client.Send(stream.CreateHairface(Info));
			break;
		}
		case Actions.Avatars:
		case Actions.HairStyles:
			Info.AvatarsOrHairTap = new Item[1];
			Info.AvatarsOrHairTap[0] = new Item();
			Info.AvatarsOrHairTap[0].Colors = new long[1];
			client.Send(stream.CreateHairface(Info));
			break;
		case Actions.Buy:
		{
			LegacyNativeHairfaceStorageTable.Hairface? hairface = LegacyNativeHairfaceStorageTable.Hairfaces.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == Info.Hair!.dwParam && (uint)i.Type == (byte)Info.Hair!.ID).FirstOrDefault();
			if (hairface != null && client.Player.Money >= hairface.Cost)
			{
				client.Player.Money -= hairface.Cost;
				client.HairfaceStorage!.Add(hairface);
			}
			break;
		}
		case Actions.Equip:
		{
			LegacyNativeHairfaceStorageTable.Hairface? hairface2 = client.HairfaceStorage!.Objects.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == Info.Hair!.dwParam && (uint)i.Type == (byte)Info.Hair!.ID).FirstOrDefault();
			if (hairface2 != null)
			{
				client.HairfaceStorage!.Equip(hairface2);
			}
			break;
		}
		case Actions.EquipColor:
			client.HairfaceStorage!.EquipColor(Info.HairColor!.ID, (byte)Info.HairColor!.dwParam);
			break;
		case Actions.BuyColor:
		{
			LegacyNativeHairfaceStorageTable.HairColor? hairColor = LegacyNativeHairfaceStorageTable.HairColors.Where((LegacyNativeHairfaceStorageTable.HairColor i) => i.ID == Info.HairColor!.ID && i.Color == Info.HairColor!.dwParam).FirstOrDefault();
			if (hairColor != null && client.Player.Money >= hairColor.Cost)
			{
				client.Player.Money -= hairColor.Cost;
				client.HairfaceStorage!.AddColor(hairColor);
			}
			break;
		}
		case Actions.Add:
		case (Actions)2:
		case Actions.AddColor:
			break;
		}
	}
}
