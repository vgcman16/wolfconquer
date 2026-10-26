using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunProofScenario(Scope scope, string scenario)
    {
        if (scenario == "ProofRawGateOnly")
        {
            var before = new GraphSnapshot();
            RequireNoProof(); before.RequireUnchanged();
            Table.m_Load = true;
            Require(Table.m_Load, "Legacy wire gate must retain its explicit compatibility assignment.");
            RequireNoProof();
            return new { RawGateOnly = true };
        }
        foreach (string family in new[] { "Jade", "Lottery", "Collection" })
        {
            string leaf = StageLeaf(family);
            scope.Write(leaf, NumericRow(leaf));
        }
        Table.Load(scope.Prefix); RequireSuccessfulLoad(scope);
        var proof = RequireProof();
        if (scenario.StartsWith("ProofFailedReload", StringComparison.Ordinal))
        {
            var before = new GraphSnapshot();
            string leaf = StageLeaf(scenario["ProofFailedReload".Length..]);
            scope.Write(leaf, leaf.StartsWith("rank-", StringComparison.Ordinal) ? "Count=1\ntruncated\n" : NumericRow(leaf) + "bad\n");
            Table.Load(scope.Prefix);
            before.RequireUnchanged();
            Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()),
                "Failed reload must retain the prior actual publication and its original current witness.");
            Require(scope.Log.ToString().Length != 0, "Failed reload must retain its caught real failure diagnostic.");
            VerifyContracts();
            return new { RetainedProof = true, Input = leaf };
        }
        if (scenario == "ProofRankRuntime")
        {
            var owner = Rank.Ranks[Rank.Type.Dragonhowl];
            var oldList = owner.RankingList;
            var entry = new Rank.Entry { UID = 101, Type = Rank.Type.Dragonhowl, Name = "live", TotalPoints = 500 };
            owner.UpdateItem(entry);
            Require(!ReferenceEquals(oldList, owner.RankingList) && ReferenceEquals(entry, owner.RankingList[101]) &&
                proof.IsCurrent() && ReferenceEquals(proof, RequireProof()),
                "Actual runtime rank replacement/list sorting must remain allowed under the same metadata witness.");
            owner.Remove(101);
            Require(owner.RankingList.Count == 0 && proof.IsCurrent() && ReferenceEquals(proof, RequireProof()),
                "Actual runtime rank removal must retain the genuine metadata witness.");
            return new { RuntimeRanks = true };
        }
        if (scenario == "ProofFilesChanged")
        {
            scope.Write("db-combat_gear.txt", "bad\n");
            Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()),
                "Published proof pins the adopted graph; later source bytes are only prospective reload inputs.");
            return new { AdoptedGraphCurrent = true };
        }
        bool setter = scenario.StartsWith("ProofSetter", StringComparison.Ordinal);
        var assignedBefore = setter ? new GraphSnapshot() : null;
        switch (scenario)
        {
            case "ProofSetterGear": var sameGear = Table.Info; Table.Info = sameGear; break;
            case "ProofSetterJade": var sameJade = Jade.Info; Jade.Info = sameJade; break;
            case "ProofSetterLottery": var sameLottery = Lottery.Info; Lottery.Info = sameLottery; break;
            case "ProofSetterCollection": var sameCollection = Collection.Info; Collection.Info = sameCollection; break;
            case "ProofSetterRanks": var sameRanks = Rank.Ranks; Rank.Ranks = sameRanks; break;
            case "ProofSetterLocation": var sameLocation = Table.Location; Table.Location = sameLocation; break;
            case "ProofSetterGate": var sameGate = Table.m_Load; Table.m_Load = sameGate; break;
            case "ProofGearRow": Table.Info[2][3].Score++; break;
            case "ProofGearSkill": Table.Info[2][3].Skills[9]++; break;
            case "ProofGearSkillsOwner": Table.Info[2][3].Skills = Table.Info[2][3].Skills.ToArray(); break;
            case "ProofGearMapOwner": Table.Info[2] = new(Table.Info[2]); break;
            case "ProofJadeRow": Jade.Info[1].TypeCombatGear++; break;
            case "ProofLotteryRow": Lottery.Info[1].rate++; break;
            case "ProofCollectionRow": Collection.Info[1].value2++; break;
            case "ProofRankShape": Rank.Ranks[Rank.Type.Dragonhowl].MaxItems++; break;
            default: throw new InvalidOperationException("Unknown load-proof scenario.");
        }
        assignedBefore?.RequireUnchanged();
        Require(!proof.IsCurrent(), "Compatibility assignment or in-place metadata drift must invalidate the retained witness: " + scenario);
        RequireNoProof();
        Require(Table.m_Load, "Legacy wire gate remains independent of mailbox metadata readiness.");
        VerifyContracts();
        return new { Stale = true, DirectSetter = setter };
    }
}
