namespace WolfConquer.Core.World;

public readonly record struct LegacyBossDeathStudyPointTeamReward(
    uint FamilyId,
    ushort KillerStudyPoints,
    ushort TeammateStudyPoints)
{
    public string FormatSystemMessage(string monsterName, string killerName)
    {
        return LegacyBossDeathRewardPolicy.FormatBossSystemMessage(monsterName, killerName, KillerStudyPoints);
    }
}

public readonly record struct LegacyBossDeathConquerPointReward(
    uint FamilyId,
    uint MapId,
    bool Boss,
    int InvocationCount,
    long PointsPerInvocation)
{
    public long TotalConquerPoints => checked(InvocationCount * PointsPerInvocation);

    public string FormatLastHitterMessage(string monsterName)
    {
        return LegacyBossDeathRewardPolicy.FormatLastHitterConquerPointMessage(monsterName, PointsPerInvocation);
    }
}

public enum LegacyMonsterDeathRandomConquerPointRewardBranch
{
    NonBossVipLadder,
    Fallback
}

public readonly record struct LegacyBossDeathHarmRankMailConquerPointReward(
    int Rank,
    long ConquerPoints,
    int ExpirationDate,
    long Money,
    uint ItemId,
    int EmoneyRecordType,
    string Title,
    string Sender,
    string Content,
    int Flag);

public readonly record struct LegacyBossDeathStudyPointInnerPowerTeamReward(
    uint FamilyId,
    bool Boss,
    ushort TeammateStudyPoints,
    int KillerInnerPowerPotency,
    int TeammateInnerPowerPotency)
{
    public string FormatSystemMessage(string monsterName, string killerName)
    {
        return LegacyBossDeathRewardPolicy.FormatBossSystemMessage(monsterName, killerName, TeammateStudyPoints);
    }
}

public readonly record struct LegacyMonsterDeathRandomConquerPointReward(
    LegacyMonsterDeathRandomConquerPointRewardBranch Branch,
    int MinimumInclusive,
    int MaximumExclusive)
{
    public int MaximumInclusive => checked(MaximumExclusive - 1);

    public bool SendsSystemMessage => Branch == LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder;

    public string? FormatSystemMessage(uint conquerPoints)
    {
        return SendsSystemMessage
            ? LegacyBossDeathRewardPolicy.FormatMonsterDeathRandomConquerPointMessage(conquerPoints)
            : null;
    }
}

public static class LegacyBossDeathRewardPolicy
{
    public const uint MegalithKingFamilyId = 3_319;
    public const uint ForestMapId = 1_011;
    public const uint ApeCityMapId = 1_020;
    public const uint MonsterDeathRandomConquerPointExcludedMapId = 2_353;
    public const int MonsterDeathRandomConquerPointMinimumInclusive = 1;
    public const int MonsterDeathFallbackRandomConquerPointMaximumExclusive = 500;
    public const int TunedNormalMonsterRandomConquerPointMaximumExclusive = 6;
    public const int TunedFallbackMonsterDeathRandomConquerPointMaximumExclusive =
        TunedNormalMonsterRandomConquerPointMaximumExclusive;
    public const int TunedMonsterDeathRandomConquerPointRatePercent = 50;
    public const uint StudyPointTeamRewardFamilyId = 20_055;
    public const ushort StudyPointTeamRewardPoints = 20;
    public const ushort StudyPointInnerPowerTeamRewardStudyPoints = 1_000;
    public const int StudyPointInnerPowerTeamRewardPotency = 1_000;
    public const uint StudyPointInnerPowerHeroRewardGoalId = 709;
    public const long SourceLastHitterBossConquerPoints = 500_000;
    public const long LastHitterBossConquerPoints = 500;
    public const long LastHitterBossConquerPointMaximumPerDeath = 1_000;
    public const uint DeityLandBossRewardMapId = 10_250;
    public const uint DragonIslandBossRewardMapId = 10_137;
    public const int HarmRankMailExpirationDate = 2_592_000;
    public const long SourceHarmRankMailMoney = 10_000_000;
    public const long HarmRankMailMoney = 100_000;
    public const long HarmRankMailConquerPointMaximum = 1_000;
    public const uint HarmRankMailItemId = 0;
    public const int HarmRankMailEmoneyRecordType = 1;
    public const string HarmRankMailSystemNameToken = "STR_SYSTEM_NAME@@";

    public static bool TryGetStudyPointTeamReward(
        uint familyId,
        bool boss,
        out LegacyBossDeathStudyPointTeamReward reward)
    {
        if (boss && familyId == StudyPointTeamRewardFamilyId)
        {
            reward = new LegacyBossDeathStudyPointTeamReward(
                familyId,
                StudyPointTeamRewardPoints,
                StudyPointTeamRewardPoints);
            return true;
        }

        reward = default;
        return false;
    }

    public static bool TryGetStudyPointInnerPowerTeamReward(
        uint familyId,
        bool boss,
        out LegacyBossDeathStudyPointInnerPowerTeamReward reward)
    {
        if ((boss && familyId == 4_171) ||
            familyId == 20_060 ||
            familyId == 8_311)
        {
            reward = new LegacyBossDeathStudyPointInnerPowerTeamReward(
                familyId,
                boss,
                StudyPointInnerPowerTeamRewardStudyPoints,
                StudyPointInnerPowerTeamRewardPotency,
                StudyPointInnerPowerTeamRewardPotency);
            return true;
        }

        reward = default;
        return false;
    }

    public static bool TryGetStudyPointInnerPowerHeroRewardGoal(
        uint familyId,
        bool boss,
        out uint goalId)
    {
        if (TryGetStudyPointInnerPowerTeamReward(familyId, boss, out _))
        {
            goalId = StudyPointInnerPowerHeroRewardGoalId;
            return true;
        }

        goalId = 0;
        return false;
    }

    public static bool TryGetConquerPointReward(
        uint familyId,
        uint mapId,
        bool boss,
        out LegacyBossDeathConquerPointReward reward)
    {
        var invocationCount = CountConquerPointRewardInvocations(familyId, mapId, boss);
        if (invocationCount > 0)
        {
            reward = new LegacyBossDeathConquerPointReward(
                familyId,
                mapId,
                boss,
                invocationCount,
                GetLastHitterBossConquerPointsPerInvocation(invocationCount));
            return true;
        }

        reward = default;
        return false;
    }

    public static long GetLastHitterBossConquerPointsPerInvocation(int invocationCount)
    {
        if (invocationCount <= 0)
        {
            return 0;
        }

        return Math.Min(
            LastHitterBossConquerPoints,
            LastHitterBossConquerPointMaximumPerDeath / invocationCount);
    }

    public static int CountConquerPointRewardInvocations(uint familyId, uint mapId, bool boss)
    {
        var count = 0;

        if (mapId == DeityLandBossRewardMapId && boss)
        {
            count++;
        }

        if (mapId == DragonIslandBossRewardMapId && familyId == 4_220)
        {
            count++;
        }

        if ((boss && familyId == 4_171) ||
            familyId == 20_060 ||
            familyId == 8_311)
        {
            count++;
        }

        if (boss && familyId == 4_212)
        {
            count++;
        }

        return count;
    }

    public static LegacyMonsterDeathRandomConquerPointReward GetMonsterDeathRandomConquerPointReward(
        uint familyId,
        uint mapId,
        bool boss,
        bool onAutoHunt,
        int vipLevel)
    {
        _ = familyId;

        if (!boss && mapId != MonsterDeathRandomConquerPointExcludedMapId && !onAutoHunt)
        {
            _ = vipLevel;

            return new LegacyMonsterDeathRandomConquerPointReward(
                LegacyMonsterDeathRandomConquerPointRewardBranch.NonBossVipLadder,
                MonsterDeathRandomConquerPointMinimumInclusive,
                TunedNormalMonsterRandomConquerPointMaximumExclusive);
        }

        return new LegacyMonsterDeathRandomConquerPointReward(
            LegacyMonsterDeathRandomConquerPointRewardBranch.Fallback,
            MonsterDeathRandomConquerPointMinimumInclusive,
            TunedFallbackMonsterDeathRandomConquerPointMaximumExclusive);
    }

    [Obsolete("Use TunedNormalMonsterRandomConquerPointMaximumExclusive; all normal maps now share the economy-safe cap.")]
    public const int TunedMap1011And1020RandomConquerPointMaximumExclusive =
        TunedNormalMonsterRandomConquerPointMaximumExclusive;

    public static bool IsTunedMap1011Or1020RandomConquerPointMap(uint mapId)
    {
        return mapId is ForestMapId or ApeCityMapId;
    }

    public static int GetMonsterDeathVipRandomConquerPointMaximumExclusive(int vipLevel)
    {
        return vipLevel switch
        {
            1 => 200,
            2 => 300,
            3 => 500,
            4 => 800,
            5 => 1_000,
            6 => 1_500,
            7 => 3_000,
            _ => 200
        };
    }

    public static bool TryGetHarmRankMailConquerPointReward(
        int rank,
        out LegacyBossDeathHarmRankMailConquerPointReward reward)
    {
        var conquerPoints = GetHarmRankMailConquerPoints(rank);
        if (conquerPoints <= 0)
        {
            reward = default;
            return false;
        }

        reward = new LegacyBossDeathHarmRankMailConquerPointReward(
            rank,
            conquerPoints,
            HarmRankMailExpirationDate,
            HarmRankMailMoney,
            HarmRankMailItemId,
            HarmRankMailEmoneyRecordType,
            HarmRankMailSystemNameToken,
            HarmRankMailSystemNameToken,
            HarmRankMailSystemNameToken,
            rank);
        return true;
    }

    public static long GetHarmRankMailConquerPoints(int rank)
    {
        var conquerPoints = rank switch
        {
            1 => 1_000,
            2 => 800,
            3 => 600,
            4 => 500,
            5 => 400,
            6 => 300,
            7 => 200,
            8 => 100,
            9 => 100,
            _ => 0
        };
        return Math.Min(conquerPoints, HarmRankMailConquerPointMaximum);
    }

    public static string FormatBossSystemMessage(string monsterName, string killerName, ushort studyPoints)
    {
        return $"The {monsterName} has been destroyed by the team {killerName}`s ! All team members received {studyPoints} Study Points!";
    }

    public static string FormatLastHitterConquerPointMessage(string monsterName, long conquerPoints)
    {
        return $"You have received {conquerPoints} Conquer Points for being the last hitter of {monsterName}!";
    }

    public static string FormatMonsterDeathRandomConquerPointMessage(uint conquerPoints)
    {
        return $"You`ve received {conquerPoints} Conquer Points.";
    }

    public static string FormatHarmRankMailMessage(string monsterName, int rank, long conquerPoints)
    {
        return
            $"You have received {conquerPoints} Conquer Points and 100 YellowRuneEssence and 100 BlueRuneEssenc And and Other Gift " +
            $"for being {rank}{GetLegacyRankSuffix(rank)} hunter while defeating {monsterName}!";
    }

    private static string GetLegacyRankSuffix(int rank)
    {
        // The decompiled source prints 2st/3st/etc.; keep the client-visible text byte-for-byte familiar.
        return "st";
    }
}
