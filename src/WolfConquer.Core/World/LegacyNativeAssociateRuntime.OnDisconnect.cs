using System.Collections.Concurrent;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public void OnDisconnect(LegacyNativePacket stream, LegacyNativeGameClientRuntime client)
		{
			LegacyNativeMsgApprenticeInformation msgApprenticeInformation = LegacyNativeMsgApprenticeInformation.Create();
			foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
			{
				foreach (KeyValuePair<byte, ConcurrentDictionary<uint, Member>> item in Associat)
				{
					if (item.Value.ContainsKey(value.Player.UID))
					{
						if (item.Key == 5)
						{
							msgApprenticeInformation.Mentor_ID = client.Player.UID;
							msgApprenticeInformation.Apprentice_ID = value.Player.UID;
							value.Player.SetMentorBattlePowers(0u, 0u);
							msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Mentor;
							msgApprenticeInformation.Online = 0u;
							msgApprenticeInformation.Class = value.Player.Class;
							msgApprenticeInformation.Mesh = value.Player.Mesh;
							msgApprenticeInformation.Level = (byte)value.Player.Level;
							msgApprenticeInformation.WriteString(client.Player.Name, value.Player.Spouse, value.Player.Name, client.Player.Signature);
							value.Send(msgApprenticeInformation.GetArray(stream));
						}
						if (item.Key == 4)
						{
							msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Apprentice;
							msgApprenticeInformation.Mentor_ID = value.Player.UID;
							msgApprenticeInformation.Apprentice_ID = client.Player.UID;
							msgApprenticeInformation.Online = 0u;
							msgApprenticeInformation.Class = client.Player.Class;
							msgApprenticeInformation.Mesh = client.Player.Mesh;
							msgApprenticeInformation.Level = (byte)client.Player.Level;
							msgApprenticeInformation.WriteString(value.Player.Name, client.Player.Spouse, client.Player.Name, client.Player.Signature);
							value.Send(msgApprenticeInformation.GetArray(stream));
						}
						if (item.Key == 1)
						{
							value.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddOffline, client.Player.UID, online: false, 0u, 0u, 0u, 0u, "", client.Player.Name));
						}
						if (item.Key == 3)
						{
							value.Send(stream.TradePartnerCreate(client.Player.UID, LegacyNativeTradePartnerPacket.Action.AddOffline, online: false, 0, client.Player.Mesh, client.Player.Level, (uint)client.Player.NobilityRank, client.Player.Signature, client.Player.Name));
						}
					}
				}
			}
		}
    }
}
