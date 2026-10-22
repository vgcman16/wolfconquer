using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNobility
{
    public partial class NobilityRanking
    {
        public ulong KnightDonation = 30000000uL;

        public ulong BaronDonation = 100000000uL;

        public ulong EarlDonation = 200000000uL;

        public ulong DukeDonation;

        public ulong PrinceDonation;

        public ulong KingDonation;

        public Dictionary<uint, LegacyNativeNobility> ClientPoll;

        public NobilityRanking()
        {
            ClientPoll = new Dictionary<uint, LegacyNativeNobility>();
        }

        public bool TryGetValue(uint UID, out LegacyNativeNobility user)
        {
            return ClientPoll.TryGetValue(UID, out user!);
        }

        public LegacyNativeNobility[] GetArray()
        {
            return ClientPoll.Values.ToArray();
        }
    }
}
