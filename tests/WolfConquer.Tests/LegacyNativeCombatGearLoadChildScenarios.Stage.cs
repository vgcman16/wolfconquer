using ProtoBuf.Meta;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunStageScenario(Scope scope, string scenario)
    {
        var before = new GraphSnapshot();
        if (scenario == "StageFull") return CompleteDetachedStage(scope, before);
        if (scenario == "StageRankOrdering") return DetachedRankOrdering(scope, before);
        if (scenario.StartsWith("StageStale", StringComparison.Ordinal))
        {
            Require(Table.MailboxLoadStage.TryCreate(scope.Prefix, scope.RankPrefix + "CombatGearRank.txt", out var stage, out var reason) &&
                stage is not null && reason == "" && stage.IsCurrentInputs(), "Complete real detached stage required before input mutation.");
            string leaf = StageLeaf(scenario["StageStale".Length..]);
            scope.Write(leaf, leaf.StartsWith("rank-", StringComparison.Ordinal) ? "Count=1\n" + RankRow() : NumericRow(leaf));
            Require(!stage!.IsCurrentInputs(), "Captured stage must reject a changed actual input: " + leaf);
            RequirePureStage(before);
            return new { Input = leaf, Stale = true };
        }
        ConfigureStageRefusal(scope, scenario);
        Require(!Table.MailboxLoadStage.TryCreate(scope.Prefix, scope.RankPrefix + "CombatGearRank.txt", out var refused, out var refusal) &&
            refused is null && refusal.Length != 0, "Incomplete/invalid real stage must return no candidate: " + scenario);
        RequirePureStage(before);
        // Exercise the real parent loader too; a detached refusal must not be
        // hidden by registration or a partial publication in the public path.
        Table.Load(scope.Prefix);
        RequireRejectedFirstLoad(scope, before);
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Input failure must precede real model discovery.");
        return new { Reason = refusal };
    }

    private static object CompleteDetachedStage(Scope scope, GraphSnapshot before)
    {
        foreach (string family in new[] { "Jade", "Lottery", "Collection" })
        {
            string leaf = StageLeaf(family);
            scope.Write(leaf, NumericRow(leaf));
        }
        scope.Write("rank-CombatGearRank.txt", "Count=2\n" + RankRow() + RankRow(102, 103, slash: true));
        Require(Table.MailboxLoadStage.TryCreate(scope.Prefix, scope.RankPrefix + "CombatGearRank.txt", out var stage, out var reason) &&
            stage is not null && reason == "" && stage.IsCurrentInputs(), "Complete five-file stage must parse without a global publication.");
        var jade = stage!.Jade[1]; var lottery = stage.Lottery[1]; var collection = stage.Collection[1];
        uint[] jadeValues = [jade.ID, (uint)jade.Type, jade.attr_type1, jade.attr_value1, jade.attr_type2, jade.attr_value2,
            jade.score, jade.points, jade.rate, jade.TypeRune, jade.Level, jade.Exp, jade.u12, jade.u13, jade.TypeCombatGear];
        uint[] lotteryValues = [lottery.Id, lottery.Type, lottery.Value1, lottery.Value2, lottery.Value3, lottery.rate];
        uint[] collectionValues = [collection.Id, collection.Type, collection.Min, collection.Mix, (uint)collection.attr_type1,
            collection.attr_value1, collection.value2];
        Require(jadeValues.SequenceEqual(Enumerable.Range(1, 15).Select(i => (uint)i)) &&
            lotteryValues.SequenceEqual(Enumerable.Range(1, 6).Select(i => (uint)i)) &&
            collectionValues.SequenceEqual(Enumerable.Range(1, 7).Select(i => (uint)i)) &&
            Values(stage.Gear[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(i => (uint)i)),
            "Every physical numeric source field must be staged in its real typed row.");
        Require(stage.Ranks.Count == 18 && stage.Ranks.All(pair => pair.Key == pair.Value._Type && pair.Value.MaxItems == 10) &&
            stage.Ranks[Rank.Type.Dragonhowl].RankingList.Keys.SequenceEqual(new uint[] { 101, 102 }) &&
            RankValues(stage.Ranks[Rank.Type.Dragonhowl].RankingList[101]).SequenceEqual(RankRow().TrimEnd('\n').Split('/')),
            "All18 genuine detached rank owners and all22rank fields must be populated; final slash is optional.");
        Require(Table.MailboxLoadStage.ReadJade(scope.Prefix + "daoqi_dict_type.txt")[1].TypeCombatGear == 15 &&
            Table.MailboxLoadStage.ReadLottery(scope.Prefix + "dict_lottery.txt")[1].rate == 6 &&
            Table.MailboxLoadStage.ReadCollection(scope.Prefix + "dict_collection.txt")[1].value2 == 7 &&
            Table.MailboxLoadStage.ReadRanksFile(scope.RankPrefix + "CombatGearRank.txt")[Rank.Type.Dragonhowl].RankingList.Count == 2,
            "Standalone staged readers must use the same complete typed parsing.");
        RequirePureStage(before);
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        Require(!ReferenceEquals(stage.Gear, Table.Info) && !ReferenceEquals(stage.Ranks, Rank.Ranks),
            "A prior detached test stage must not be accepted as authority by the actual public load.");
        return new { GearColumns = 19, JadeColumns = 15, LotteryColumns = 6, CollectionColumns = 7, RankColumns = 22 };
    }

    private static object DetachedRankOrdering(Scope scope, GraphSnapshot before)
    {
        string rows = string.Concat(Enumerable.Range(0, 12).Select(i => RankRow((uint)(200 + i), (uint)(i + 1), slash: i % 2 == 0)));
        scope.Write("rank-CombatGearRank.txt", "Count=13\n" + rows + RankRow(211, 100));
        Require(Table.MailboxLoadStage.TryCreate(scope.Prefix, scope.RankPrefix + "CombatGearRank.txt", out var stage, out var reason) &&
            stage is not null && reason == "", "Complete rank ordering stage required.");
        var entries = stage!.Ranks[Rank.Type.Dragonhowl].RankingList;
        Require(entries.Count == 10 && entries.Keys.SequenceEqual(new uint[] { 211, 210, 209, 208, 207, 206, 205, 204, 203, 202 }) &&
            entries[211].TotalPoints == 100, "Real Rank.UpdateItem must retain UID replacement, descending scores and top-ten trimming.");
        RequirePureStage(before);
        return new { Entries = entries.Count };
    }

    private static void ConfigureStageRefusal(Scope scope, string scenario)
    {
        if (scenario.StartsWith("StageMissing", StringComparison.Ordinal))
        { scope.Remove(StageLeaf(scenario["StageMissing".Length..])); return; }
        if (scenario.StartsWith("StageExtra", StringComparison.Ordinal))
        {
            string leaf = StageLeaf(scenario["StageExtra".Length..]);
            scope.Write(leaf, NumericRow(leaf).TrimEnd('\n') + "@@0\n"); return;
        }
        if (scenario.StartsWith("StageDuplicate", StringComparison.Ordinal) && scenario != "StageDuplicateGearBadTail")
        {
            string leaf = StageLeaf(scenario["StageDuplicate".Length..]);
            scope.Write(leaf, NumericRow(leaf) + NumericRow(leaf)); return;
        }
        switch (scenario)
        {
            case "StageDuplicateGearBadTail":
                string bad = GearRow(17, 2, 3).TrimEnd('\n');
                scope.Write("db-combat_gear.txt", GearRow() + bad[..bad.LastIndexOf("@@", StringComparison.Ordinal)] + "@@bad\n"); break;
            case "StageRankMissingHeader": scope.Write("rank-CombatGearRank.txt", ""); break;
            case "StageRankCountMismatch": scope.Write("rank-CombatGearRank.txt", "Count=2\n" + RankRow()); break;
            case "StageRankTruncated": scope.Write("rank-CombatGearRank.txt", "Count=1\n" + RankRow().TrimEnd('\n')[..^4] + "\n"); break;
            case "StageRankExtra": scope.Write("rank-CombatGearRank.txt", "Count=1\n" + RankRow().TrimEnd('\n') + "/extra\n"); break;
            case "StageRankUnknownType": scope.Write("rank-CombatGearRank.txt", "Count=1\n" + RankRow().Replace("/20/", "/255/", StringComparison.Ordinal)); break;
            case "StageRankByteOverflow": scope.Write("rank-CombatGearRank.txt", "Count=1\n" + RankRow().Replace("/106/", "/256/", StringComparison.Ordinal)); break;
            default: throw new InvalidOperationException("Unknown detached-stage refusal scenario.");
        }
    }

    private static string StageLeaf(string family) => family switch
    {
        "Gear" => "db-combat_gear.txt", "Jade" => "db-daoqi_dict_type.txt", "Lottery" => "db-dict_lottery.txt",
        "Collection" => "db-dict_collection.txt", "Ranks" => "rank-CombatGearRank.txt",
        _ => throw new InvalidOperationException("Unknown actual source input family."),
    };

    private static string NumericRow(string leaf) => leaf switch
    {
        "db-daoqi_dict_type.txt" => string.Join("@@", Enumerable.Range(1, 15)) + "\n",
        "db-dict_lottery.txt" => string.Join("@@", Enumerable.Range(1, 6)) + "\n",
        "db-dict_collection.txt" => string.Join("@@", Enumerable.Range(1, 7)) + "\n",
        "db-combat_gear.txt" => GearRow(17, 8, 9),
        _ => throw new InvalidOperationException("Unknown numeric source row."),
    };

    private static void RequirePureStage(GraphSnapshot before)
    {
        before.RequireUnchanged(); RequireNoProof();
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(),
            "Pure detached staging must not register/freeze serializer models or publish any global owner.");
    }
}
