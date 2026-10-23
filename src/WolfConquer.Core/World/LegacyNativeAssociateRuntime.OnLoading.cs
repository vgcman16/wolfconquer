using System.Collections.Concurrent;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public void OnLoading(LegacyNativeGameClientRuntime client)
		{
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			LegacyNativeMsgApprenticeInformation msgApprenticeInformation = LegacyNativeMsgApprenticeInformation.Create();
			foreach (KeyValuePair<byte, ConcurrentDictionary<uint, Member>> item in Associat)
			{
				foreach (Member value7 in item.Value.Values)
				{
					if (item.Key == 5)
					{
						if (LegacyNativePoolClients.GamePoll!.TryGetValue(value7.UID, out var value))
						{
							if (client.Player.Associate!.OnlineApprentice.TryAdd(value.Player.UID, value))
							{
								Member member = Associat[5][value.Player.UID];
								msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Apprentice;
								msgApprenticeInformation.Mentor_ID = client.Player.UID;
								msgApprenticeInformation.Apprentice_ID = value.Player.UID;
								msgApprenticeInformation.Apprentice_Blessing = (ushort)member.Blessing;
								msgApprenticeInformation.Apprentice_Composing = (ushort)member.Stone;
								msgApprenticeInformation.Apprentice_Experience = (ushort)member.ExpBalls;
								msgApprenticeInformation.Class = value.Player.Class;
								msgApprenticeInformation.Enrole_date = (uint)member.Timer;
								msgApprenticeInformation.Mesh = value.Player.Mesh;
								msgApprenticeInformation.Level = (byte)value.Player.Level;
								msgApprenticeInformation.Online = 1u;
								msgApprenticeInformation.PkPoints = value.Player.PKPoints;
								msgApprenticeInformation.WriteString(client.Player.Name, value.Player.Spouse, value.Player.Name, value.Player.Signature);
								client.Send(msgApprenticeInformation.GetArray(stream));
								value.Player.SetMentorBattlePowers(client.Player.GetShareBattlePowers((uint)value.Player.RealBattlePower), (uint)client.Player.RealBattlePower);
								msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Mentor;
								msgApprenticeInformation.Class = client.Player.Class;
								msgApprenticeInformation.Enrole_date = (uint)member.Timer;
								msgApprenticeInformation.Mesh = client.Player.Mesh;
								msgApprenticeInformation.Level = (byte)client.Player.Level;
								msgApprenticeInformation.Online = 1u;
								msgApprenticeInformation.PkPoints = client.Player.PKPoints;
								msgApprenticeInformation.Shared_Battle_Power = client.Player.GetShareBattlePowers((uint)value.Player.RealBattlePower);
								msgApprenticeInformation.WriteString(client.Player.Name, value.Player.Spouse, value.Player.Name, value.Player.Signature);
								value.Send(msgApprenticeInformation.GetArray(stream));
							}
						}
						else
						{
							msgApprenticeInformation.Class = value7.Class;
							msgApprenticeInformation.Mesh = value7.Mesh;
							msgApprenticeInformation.Level = value7.Level;
							msgApprenticeInformation.Online = 0u;
							msgApprenticeInformation.PkPoints = 0;
							msgApprenticeInformation.Shared_Battle_Power = 0u;
							msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Apprentice;
							msgApprenticeInformation.Mentor_ID = MyUID;
							msgApprenticeInformation.Apprentice_ID = value7.UID;
							msgApprenticeInformation.Enrole_date = (uint)value7.Timer;
							msgApprenticeInformation.WriteString("NULL", "NULL", value7.Name, value7.Signature);
							client.Send(msgApprenticeInformation.GetArray(stream));
						}
					}
					if (item.Key == 4)
					{
						if (LegacyNativePoolClients.GamePoll!.TryGetValue(value7.UID, out var value2))
						{
							if (value2.Player.Associate!.Associat[5].TryGetValue(client.Player.UID, out var value3) && value2.Player.Associate!.OnlineApprentice.TryAdd(client.Player.UID, client))
							{
								msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Apprentice;
								msgApprenticeInformation.Mentor_ID = value2.Player.UID;
								msgApprenticeInformation.Apprentice_ID = client.Player.UID;
								msgApprenticeInformation.Apprentice_Blessing = (ushort)value3.Blessing;
								msgApprenticeInformation.Apprentice_Composing = (ushort)value3.Stone;
								msgApprenticeInformation.Apprentice_Experience = (ushort)value3.ExpBalls;
								msgApprenticeInformation.Class = client.Player.Class;
								msgApprenticeInformation.Enrole_date = (uint)value3.Timer;
								msgApprenticeInformation.Mesh = client.Player.Mesh;
								msgApprenticeInformation.Level = (byte)client.Player.Level;
								msgApprenticeInformation.Online = 1u;
								msgApprenticeInformation.PkPoints = client.Player.PKPoints;
								msgApprenticeInformation.WriteString(value2.Player.Name, client.Player.Spouse, client.Player.Name, client.Player.Signature);
								value2.Send(msgApprenticeInformation.GetArray(stream));
								client.Player.SetMentorBattlePowers(value2.Player.GetShareBattlePowers((uint)client.Player.RealBattlePower), (uint)value2.Player.RealBattlePower);
								msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Mentor;
								msgApprenticeInformation.Class = value2.Player.Class;
								msgApprenticeInformation.Enrole_date = (uint)value3.Timer;
								msgApprenticeInformation.Mesh = value2.Player.Mesh;
								msgApprenticeInformation.Level = (byte)value2.Player.Level;
								msgApprenticeInformation.Online = 1u;
								msgApprenticeInformation.PkPoints = value2.Player.PKPoints;
								msgApprenticeInformation.Shared_Battle_Power = value2.Player.GetShareBattlePowers((uint)client.Player.RealBattlePower);
								msgApprenticeInformation.WriteString(value2.Player.Name, client.Player.Spouse, client.Player.Name, client.Player.Signature);
								client.Send(msgApprenticeInformation.GetArray(stream));
							}
						}
						else
						{
							msgApprenticeInformation.Class = value7.Class;
							msgApprenticeInformation.Mesh = value7.Mesh;
							msgApprenticeInformation.Level = value7.Level;
							msgApprenticeInformation.Online = 0u;
							msgApprenticeInformation.PkPoints = 0;
							msgApprenticeInformation.Shared_Battle_Power = 0u;
							msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Mentor;
							msgApprenticeInformation.Mentor_ID = value7.UID;
							msgApprenticeInformation.Apprentice_ID = MyUID;
							msgApprenticeInformation.Enrole_date = (uint)value7.Timer;
							msgApprenticeInformation.WriteString(value7.Name, "", "", "");
							client.Send(msgApprenticeInformation.GetArray(stream));
						}
					}
					if (item.Key == 1)
					{
						if (LegacyNativePoolClients.GamePoll!.TryGetValue(value7.UID, out var value4))
						{
							client.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddFriend, value7.UID, online: true, (uint)value4.Player.NobilityRank, 0u, value4.Player.Mesh, value4.Player.Level, value4.Player.Signature, value7.Name));
							value4.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddOnline, client.Player.UID, online: true, (uint)client.Player.NobilityRank, 0u, client.Player.Mesh, client.Player.Level, client.Player.Signature, client.Player.Name));
						}
						else
						{
							client.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddFriend, value7.UID, online: false, 0u, 0u, 0u, 0u, "", value7.Name));
						}
					}
					if (item.Key == 2)
					{
						if (LegacyNativePoolClients.GamePoll!.TryGetValue(value7.UID, out var value5))
						{
							client.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddEnemy, value7.UID, online: true, (uint)value5.Player.NobilityRank, 0u, value5.Player.Mesh, value5.Player.Level, value5.Player.Signature, value7.Name));
						}
						else
						{
							client.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddEnemy, value7.UID, online: false, 0u, 0u, 0u, 0u, "", value7.Name));
						}
					}
					if (item.Key == 3)
					{
						if (LegacyNativePoolClients.GamePoll!.TryGetValue(value7.UID, out var value6))
						{
							client.Send(stream.TradePartnerCreate(value7.UID, LegacyNativeTradePartnerPacket.Action.AddPartner, online: true, value7.GetTimerLeft(), value7.Mesh, value7.Level, value7.NobilityRank, value7.Signature, value7.Name));
							value6.Send(stream.TradePartnerCreate(client.Player.UID, LegacyNativeTradePartnerPacket.Action.AddOnline, online: true, value7.GetTimerLeft(), client.Player.Mesh, client.Player.Level, (uint)client.Player.NobilityRank, client.Player.Signature, client.Player.Name));
						}
						else
						{
							client.Send(stream.TradePartnerCreate(value7.UID, LegacyNativeTradePartnerPacket.Action.AddPartner, online: false, value7.GetTimerLeft(), value7.Mesh, value7.Level, value7.NobilityRank, value7.Signature, value7.Name));
						}
					}
				}
			}
		}
    }
}
