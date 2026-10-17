namespace WolfConquer.Core.World;

public enum LegacyBossHarmRankPackageClaimPlanStatus
{
    Success = 0,
    InvalidRank,
    InventoryFull
}

public readonly record struct LegacyBossHarmRankPackageClaimPlan(
    LegacyBossHarmRankPackageClaimPlanStatus Status,
    int Rank,
    IReadOnlyList<LegacyBossHarmRankPackageItem> ItemGrants,
    uint ClassExperience,
    uint NinjaPoints,
    uint PirateAirPowerPoints,
    uint PirateExperience,
    int RequiredInventorySlots,
    int AvailableInventorySlots)
{
    public bool CanClaim => Status == LegacyBossHarmRankPackageClaimPlanStatus.Success;

    public static bool TryCreate(
        uint rankFlag,
        long classCode,
        int availableInventorySlots,
        out LegacyBossHarmRankPackageClaimPlan plan)
    {
        if (!LegacyBossHarmRankPackagePolicy.TryGetPackage((int)rankFlag, out var reward))
        {
            plan = new LegacyBossHarmRankPackageClaimPlan(
                LegacyBossHarmRankPackageClaimPlanStatus.InvalidRank,
                Rank: 0,
                ItemGrants: Array.Empty<LegacyBossHarmRankPackageItem>(),
                ClassExperience: 0,
                NinjaPoints: 0,
                PirateAirPowerPoints: 0,
                PirateExperience: 0,
                RequiredInventorySlots: 0,
                AvailableInventorySlots: availableInventorySlots);
            return false;
        }

        var job = LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode);
        var itemGrants = reward.Items;
        var ninjaPoints = 0U;
        var pirateAirPowerPoints = 0U;
        var pirateExperience = 0U;

        if (LegacyBossHarmRankPackagePolicy.TryGetJobReward(reward, job, out var jobReward))
        {
            itemGrants = reward.Items.Concat(jobReward.Items).ToArray();
            ninjaPoints = jobReward.NinjaPoints;
            pirateAirPowerPoints = jobReward.PirateAirPowerPoints;
            pirateExperience = jobReward.PirateExperience;
        }

        var requiredInventorySlots = itemGrants.Count;
        var status = availableInventorySlots >= requiredInventorySlots
            ? LegacyBossHarmRankPackageClaimPlanStatus.Success
            : LegacyBossHarmRankPackageClaimPlanStatus.InventoryFull;

        plan = new LegacyBossHarmRankPackageClaimPlan(
            status,
            reward.Rank,
            itemGrants,
            reward.ClassExperience,
            ninjaPoints,
            pirateAirPowerPoints,
            pirateExperience,
            requiredInventorySlots,
            availableInventorySlots);

        return status == LegacyBossHarmRankPackageClaimPlanStatus.Success;
    }
}
