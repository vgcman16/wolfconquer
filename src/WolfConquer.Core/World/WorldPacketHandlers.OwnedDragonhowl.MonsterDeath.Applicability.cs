using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // A selected-input DATA check, not permission to run any source death body.
    // Applicable unjoined subsystems refuse before proc cost/damage. A future
    // real owner replaces its particular hold; no accepting callback belongs here.
    private sealed class DragonhowlMonsterRewardApplicability
    {
        private readonly PlayerQuestSnapshot quests;
        private readonly IReadOnlyList<PlayerQuestSnapshotEntry> rows;
        private readonly (uint Id, PlayerQuestSnapshotEntry? Row)[] usedQuests;

        private DragonhowlMonsterRewardApplicability(PlayerQuestSnapshot quests,
            (uint Id, PlayerQuestSnapshotEntry? Row)[] usedQuests)
        { this.quests = quests; rows = quests.Quests; this.usedQuests = usedQuests; }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession actor,
            LegacyMonsterInstance monster, Monster family,
            out DragonhowlMonsterRewardApplicability? proof, out string reason)
        {
            proof = null; reason = "ownedDragonhowlMonsterRewardInputsInvalid";
            if (context.WorldData is null || monster.MapId < 0 || family.Id < 0 || monster.MonsterId != family.Id ||
                actor.Character.PlayerQuests is not { } quests || !IsPlain(quests.Quests) ||
                quests.Quests.Any(row => row is null)) return false;
            reason = UnjoinedBranch(monster, family);
            if (reason.Length != 0) return false;

            // Quest 2375 applies on every kill. The Twin City branches below
            // read only these actual quest IDs; unused quests are not a gate.
            uint[] selected = monster.MapId == 1_002 ? family.Id switch
            {
                1 => [2_375, 1_307, 824],
                2 => [2_375, 1_007],
                3 => [2_375, 1_735, 1_009, 2_365, 1_010],
                4 => [2_375, 1_013, 1_016],
                _ => [2_375],
            } : [2_375];
            var used = new (uint Id, PlayerQuestSnapshotEntry? Row)[selected.Length];
            for (int index = 0; index < selected.Length; index++)
            {
                var matches = quests.Quests.Where(row => row is not null && row.Uid == selected[index]).Take(2).ToArray();
                if (matches.Length > 1)
                { reason = "ownedDragonhowlMonsterRewardQuestAmbiguous"; return false; }
                var row = matches.SingleOrDefault();
                if (row is not null && row.Status is not (PlayerQuestStatus.Accepted or PlayerQuestStatus.Finished or PlayerQuestStatus.Available))
                { reason = "ownedDragonhowlMonsterRewardQuestStatusInvalid"; return false; }
                if (row?.Status == PlayerQuestStatus.Accepted)
                { reason = "ownedDragonhowlUnjoined:monsterDeathQuestOwner:" + selected[index]; return false; }
                used[index] = (selected[index], row);
            }
            var captured = new DragonhowlMonsterRewardApplicability(quests, used);
            if (!captured.IsCurrentPlain())
            { reason = "ownedDragonhowlMonsterRewardQuestChanged"; return false; }
            proof = captured; reason = string.Empty; return true;
        }

        internal bool IsCurrentPlain()
        {
            if (!ReferenceEquals(quests.Quests, rows) || !IsPlain(rows) || rows.Any(row => row is null)) return false;
            foreach (var used in usedQuests)
            {
                var selected = rows.Where(row => row is not null && row.Uid == used.Id).Take(2).ToArray();
                if (selected.Length != (used.Row is null ? 0 : 1) ||
                    used.Row is not null && !ReferenceEquals(selected[0], used.Row)) return false;
            }
            return true;
        }

        private static bool IsPlain(IReadOnlyList<PlayerQuestSnapshotEntry> rows) =>
            rows.GetType() == typeof(PlayerQuestSnapshotEntry[]) || rows.GetType() == typeof(List<PlayerQuestSnapshotEntry>);

        private static string UnjoinedBranch(LegacyMonsterInstance monster, Monster family)
        {
            if (monster.Boss) return "ownedDragonhowlUnjoined:monsterBossHarmRankMail";
            uint id = checked((uint)family.Id);
            if (LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerTeamReward(id, monster.Boss, out _) ||
                LegacyBossDeathRewardPolicy.TryGetStudyPointInnerPowerHeroRewardGoal(id, monster.Boss, out _) ||
                LegacyBossDeathRewardPolicy.TryGetStudyPointTeamReward(id, monster.Boss, out _))
                return "ownedDragonhowlUnjoined:monsterStudyInnerPowerHeroReward";
            if (LegacyBossSpecialDeathDropPolicy.IsQueenOfEvilSpecialBranch(id, family.Name))
                return "ownedDragonhowlUnjoined:queenClassReward";
            if (family.Id is 3_319 or 4_212 || TryGetLegacyHouseDemonRewardFamily(family.Id, out _) ||
                family.Id == 8_418 && monster.MapId == 10_342)
                return "ownedDragonhowlUnjoined:monsterFamilyQuestOrInventoryReward";

            if (monster.MapId == LegacyInstanceInfoMapId || TryGetLegacyBattleFieldMonsterDeathPoints(monster.MapId, out _))
                return "ownedDragonhowlUnjoined:monsterEventDeathOwner";
            if (monster.MapId is 1_780 or 2_022 or 2_353 or 3_831 or 3_833 or 3_834 or 3_935 or 3_998 or 10_250 or 44_456 or 44_462 ||
                monster.MapId == 44_461 && family.Id == 452_373 || monster.MapId == 1_791 && family.Id == 2_172 ||
                monster.MapId == 2_024 && family.Id == 66_432 || monster.MapId == 10_428 && family.Id == 2_984 ||
                monster.MapId == 3_071 && family.Id is (2_700 or 7_022) ||
                monster.MapId == 1_013 && family.Id == 14_333)
                return "ownedDragonhowlUnjoined:monsterMapDeathOwner";

            // These compound source blocks interleave quest rewards, generated
            // item bodies and dynamic spawns. Their concrete owners/no-op
            // extraction have not joined yet. This is an implementation hold,
            // not an assertion that every family in these maps earns a reward.
            return monster.MapId is 1_000 or 1_001 or 1_011 or 1_015 or 1_020
                ? "ownedDragonhowlUnjoined:compoundMonsterMapRewardOwner"
                : string.Empty;
        }
    }
}
