using System.Globalization;
using ProtoBuf.Meta;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object? RunLoaderScenario(Scope scope, string scenario)
    {
        var before = new GraphSnapshot();
        switch (scenario)
        {
            case "ColumnsAndDuplicates":
                scope.Write("db-combat_gear.txt", "@@  " + string.Join("@@@@  ", Enumerable.Range(1, 19)) + "@@\n" +
                    GearRow(999, 2, 3) + GearRow(17, 2, 4) + GearRow(23, 9, 3));
                Table.Load(scope.Prefix);
                RequireSuccessfulLoad(scope);
                Require(!ReferenceEquals(scope.OldInfo, Table.Info) && scope.OldInfo[999][999].ID == 999,
                    "Escaped old gear graph must survive replacement.");
                Require(Table.Info.Count == 2 && Table.Info[2].Count == 2 &&
                    Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)),
                    "All19 source columns and first complete duplicate winner are required.");
                Require(Table.Info[2][4].ID == 17 && Table.Info[9][3].ID == 23 && !Table.Info.ContainsKey(1),
                    "Type rather than row ID must remain the outer key.");
                var proof = RequireProof();
                var alias = Table.Info[2][3]; alias.Score = 777;
                Require(ReferenceEquals(alias, Table.Info[2][3]) && Table.Info[2][3].Score == 777,
                    "Legacy public row aliases must retain their actual mutable semantics.");
                Require(!proof.IsCurrent(), "In-place public row mutation must invalidate the retained proof.");
                RequireNoProof();
                break;
            case "Culture":
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.PositiveSign = "p";
                CultureInfo.CurrentCulture = culture;
                scope.Write("db-combat_gear.txt", string.Join("@@", Enumerable.Range(1, 19).Select(i => "p" + i)));
                Table.Load(scope.Prefix);
                RequireSuccessfulLoad(scope);
                Require(Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)) &&
                    ReferenceEquals(culture, CultureInfo.CurrentCulture), "All19 UInt32 values must use the captured current culture.");
                break;
            case "RawPrefix":
                string raw = scope.Prefix.TrimEnd('-');
                Table.Load(raw);
                RequireRejectedFirstLoad(scope, before);
                Require(File.Exists(scope.GearFile), "Loader must not repair the caller's raw prefix.");
                break;
            case "MissingGear":
                scope.RemoveGear();
                Table.Load(scope.Prefix);
                RequireRejectedFirstLoad(scope, before);
                break;
            case "EmptyGear":
                scope.Write("db-combat_gear.txt", "");
                Table.Load(scope.Prefix);
                RequireSuccessfulLoad(scope);
                Require(Table.Info.Count == 0, "A complete existing empty gear file is valid empty metadata.");
                break;
            case "SharedGear":
                using (var lease = File.Open(scope.GearFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Table.Load(scope.Prefix);
                RequireRejectedFirstLoad(scope, before);
                break;
            case "LateBlank":
            case "LateFormat":
            case "LateOverflow":
                string bad = scenario == "LateBlank" ? "" : GearRow(17, 8, 9).TrimEnd('\n');
                if (scenario != "LateBlank") bad = bad[..bad.LastIndexOf("@@", StringComparison.Ordinal)] +
                    (scenario == "LateFormat" ? "@@bad" : "@@4294967296");
                scope.Write("db-combat_gear.txt", GearRow() + bad + "\n" + GearRow(23, 9, 4));
                Table.Load(scope.Prefix);
                RequireRejectedFirstLoad(scope, before);
                break;
            case "DependenciesContinue":
                scope.Write("db-daoqi_dict_type.txt", string.Join("@@", Enumerable.Range(1, 15)) + "\nbad\n");
                scope.Write("db-dict_lottery.txt", "42@@1@@2@@3@@4@@5\n");
                scope.Write("db-dict_collection.txt", "bad\n");
                Table.Load(scope.Prefix);
                RequireRejectedFirstLoad(scope, before);
                break;
            case "RankColumnsAndPrefix":
                scope.Write("rank-CombatGearRank.txt", "Count=1\n" + RankRow());
                scope.Write("db-CombatGearRank.txt", "Count=0\n");
                Table.Load(scope.Prefix);
                RequireSuccessfulLoad(scope);
                var entry = Rank.Ranks[Rank.Type.Dragonhowl].RankingList[101];
                Require(RankValues(entry).SequenceEqual(RankRow().TrimEnd('\n').Split('/')),
                    "All22 rank columns must come from the separately configured raw rank prefix.");
                break;
            case "RegisteredWire":
                Table.Load(scope.Prefix);
                RequireSuccessfulLoad(scope);
                return VerifyRegisteredWire();
            case "SeparatorFault":
                scope.Write("db-combat_gear.txt", "1\t2@@" + string.Join("@@", Enumerable.Range(3, 17)));
                Table.Load(scope.Prefix);
                RequireRejectedFirstLoad(scope, before);
                break;
            default: throw new InvalidOperationException("Unknown isolated gear-loader scenario.");
        }
        if (!Table.m_Load)
            Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(),
                "Failed input preparation must precede any real serializer-model registration.");
        return null;
    }
}
