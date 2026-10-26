using System.Text.Json;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private sealed class GraphSnapshot
    {
        private readonly object[] owners = GraphOwners();
        private readonly string state = GraphState();
        private readonly string location = Table.Location;
        private readonly bool rawGate = Table.m_Load;

        internal void RequireUnchanged()
        {
            var current = GraphOwners();
            Require(owners.Length == current.Length && owners.Zip(current).All(pair => ReferenceEquals(pair.First, pair.Second)),
                "Rejected load changed a family/map/row/Skills/rank/list/entry owner.");
            Require(state == GraphState(), "Rejected load changed an escaped graph value or dictionary order.");
            Require(location == Table.Location && rawGate == Table.m_Load, "Rejected load changed prior Location or raw wire gate.");
        }
    }

    private static object[] GraphOwners()
    {
        var result = new List<object> { Table.Info, Jade.Info, Lottery.Info, Collection.Info, Rank.Ranks };
        foreach (var levels in Table.Info.Values)
        {
            result.Add(levels);
            foreach (var row in levels.Values)
            {
                result.Add(row);
                if (row.Skills is not null) result.Add(row.Skills);
            }
        }
        result.AddRange(Jade.Info.Values); result.AddRange(Lottery.Info.Values); result.AddRange(Collection.Info.Values);
        foreach (var rank in Rank.Ranks.Values)
        {
            result.Add(rank); result.Add(rank.SynRoot); result.Add(rank.RankingList); result.AddRange(rank.RankingList.Values);
        }
        return result.ToArray();
    }

    private static string GraphState() => JsonSerializer.Serialize(new
    {
        Gear = Table.Info, Jade = Jade.Info, Lottery = Lottery.Info, Collection = Collection.Info,
        Ranks = Rank.Ranks.Select(pair => new { Key = pair.Key, pair.Value._Type, pair.Value.MaxItems,
            Entries = pair.Value.RankingList }),
    }, new JsonSerializerOptions { IncludeFields = true });

    private static void RequireRejectedFirstLoad(Scope scope, GraphSnapshot before)
    {
        before.RequireUnchanged();
        RequireNoProof();
        Require(!Table.m_Load && Table.Location == "old-location" && scope.Log.ToString().Length != 0,
            "Failed first load must preserve its closed prestate and report the real load failure.");
    }

    private static void RequireSuccessfulLoad(Scope scope)
    {
        RequireProof(); VerifyContracts();
        Require(Table.m_Load && Table.Location == scope.Prefix && Rank.Ranks.Count == 18 &&
            Rank.Ranks.All(pair => pair.Key == pair.Value._Type && pair.Value.MaxItems == 10),
            "Success requires one complete actual five-family publication and its raw prefix.");
        Require(scope.Log.ToString() == "", "Successful complete load must not conceal a caught fault.");
    }

    private static string[] RankValues(Rank.Entry e) => [e.UID.ToString(), e.Name, ((uint)e.Type).ToString(), e.TotalPoints.ToString(),
        e.Class.ToString(), e.Level.ToString(), e.Mesh.ToString(), e.HairStyle.ToString(), e.Head.ToString(), e.Garment.ToString(),
        e.LeftWeapon.ToString(), e.LefttWeaponAccessory.ToString(), e.RightWeapon.ToString(), e.RightWeaponAccessory.ToString(),
        e.MountArmor.ToString(), e.Armor.ToString(), e.Wing.ToString(), e.WingPlus.ToString(), e.Title.ToString(), e.Flag.ToString(), e.GuildName, e.WeaponWar.ToString()];

    private static string RankRow(uint uid = 101, uint points = 104, bool slash = false)
    {
        string[] fields = [uid.ToString(), "Wolf", "20", points.ToString(), "105", "106", "107", "108", "109", "110", "111",
            "112", "113", "114", "115", "116", "117", "118", "119", "120", "Guild", "122"];
        return string.Join('/', fields) + (slash ? "/" : "") + "\n";
    }
}
