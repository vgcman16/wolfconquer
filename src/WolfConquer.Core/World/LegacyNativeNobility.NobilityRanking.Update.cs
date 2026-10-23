using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNobility
{
    public partial class NobilityRanking
    {
        public void RemoveAndUpdateTheRest(uint UID)
        {
            ClientPoll.Remove(UID);
            LegacyNativeNobility[] array = GetArray();
            foreach (LegacyNativeNobility user in array)
            {
                UpdateRank(user);
            }
        }

        public void UpdateRank(LegacyNativeNobility user)
        {
            if (ClientPoll.Count < 50)
            {
                CreateRank(user);
                return;
            }
            LegacyNativeNobility[] array = ClientPoll.Values.ToArray();
            if (array[49].Donation < user.Donation)
            {
                CreateRank(user);
            }
            else if (ClientPoll.ContainsKey(user.UID))
            {
                CreateRank(user);
            }
        }
    }
}
