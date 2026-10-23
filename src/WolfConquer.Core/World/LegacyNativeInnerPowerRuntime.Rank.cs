using System.Collections.Concurrent;
using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Whole native InnerPowerRank14..65. Source never removes rank100 when adding a better player.
    public static class InnerPowerRank
    {
        public const int MaxPlayers = 100;
        private static ConcurrentDictionary<uint, LegacyNativeInnerPowerRuntime> InnerPowerRanks = new();
        private static LegacyNativeInnerPowerRuntime[]? Rankings;
        private static object synroot = new object();

        public static void UpdateRank(LegacyNativeInnerPowerRuntime inner)
        {
            if (InnerPowerRanks.Count < 100)
            {
                InnerPowerRanks.TryAdd(inner.UID, inner);
                CreateRanks();
                return;
            }
            LegacyNativeInnerPowerRuntime innerPower = Rankings!.Last();
            if (innerPower.TotalScore < inner.TotalScore)
            {
                InnerPowerRanks.TryAdd(inner.UID, inner);
                CreateRanks();
            }
            else if (InnerPowerRanks.ContainsKey(inner.UID))
            {
                CreateRanks();
            }
        }

        public static void CreateRanks()
        {
            lock (synroot)
            {
                LegacyNativeInnerPowerRuntime[] source = InnerPowerRanks.Values.ToArray();
                Rankings = (from p in source
                    where p.TotalScore != 0
                    orderby p.TotalScore descending
                    select p).ToArray();
            }
        }

        public static LegacyNativeInnerPowerRuntime[] GetRankingList()
        {
            LegacyNativeInnerPowerRuntime[]? result = null;
            lock (synroot)
            {
                result = Rankings!.ToArray();
            }
            return result;
        }
    }
}
