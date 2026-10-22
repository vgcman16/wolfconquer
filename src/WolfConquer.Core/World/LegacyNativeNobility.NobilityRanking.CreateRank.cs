using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNobility
{
    public partial class NobilityRanking
    {
        internal void CreateRank(LegacyNativeNobility user)
        {
            lock (ClientPoll)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                if (!ClientPoll.ContainsKey(user.UID))
                {
                    ClientPoll.Add(user.UID, user);
                }
                LegacyNativeNobility[] source = ClientPoll.Values.Where((LegacyNativeNobility p) => p.Donation > 200000000).ToArray();
                LegacyNativeNobility[] array = source.OrderByDescending((LegacyNativeNobility p) => p.Donation).ToArray();
                ClientPoll.Clear();
                for (int num = 0; num < array.Length; num++)
                {
                    LegacyNativeNobility nobility = array[num];
                    int position = nobility.Position;
                    LegacyNativeNobilityRank rank = nobility.Rank;
                    if (num < 50)
                    {
                        nobility.Position = num;
                        ClientPoll.Add(nobility.UID, nobility);
                        switch (num)
                        {
                            case 2:
                                KingDonation = nobility.Donation;
                                break;
                            case 14:
                                PrinceDonation = nobility.Donation;
                                break;
                            case 49:
                                DukeDonation = nobility.Donation;
                                break;
                        }
                    }
                    else
                    {
                        nobility.Position = -1;
                    }
                    if (position != nobility.Position)
                    {
                        UpdateClientIcon(stream, nobility);
                        if ((int)nobility.Rank > (int)rank && (int)nobility.Rank > (int)rank)
                        {
                            LegacyNativeServerBroadcast.SendGlobalPacket(new LegacyNativeMsgMessage(nobility.Name + " has been promoted to " + nobility.Rank.ToString() + ".", "ALL", LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Center).GetArray(stream));
                        }
                    }
                }
            }
        }
    }
}
