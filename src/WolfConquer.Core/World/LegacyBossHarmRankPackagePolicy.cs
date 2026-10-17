namespace WolfConquer.Core.World;

public enum LegacyBossHarmRankPackageJob
{
    Other = 0,
    Trojan,
    Warrior,
    Archer,
    Ninja,
    Pirate
}

public readonly record struct LegacyBossHarmRankPackageItem(
    uint ItemId,
    ushort Plus,
    uint Quantity,
    bool Bound,
    int SocketProgress);

public readonly record struct LegacyBossHarmRankPackageJobReward(
    LegacyBossHarmRankPackageJob Job,
    IReadOnlyList<LegacyBossHarmRankPackageItem> Items,
    uint NinjaPoints,
    uint PirateAirPowerPoints,
    uint PirateExperience);

public readonly record struct LegacyBossHarmRankPackageReward(
    int Rank,
    IReadOnlyList<LegacyBossHarmRankPackageItem> Items,
    uint ClassExperience,
    IReadOnlyList<LegacyBossHarmRankPackageJobReward> JobRewards)
{
    public bool HasPirateReward =>
        JobRewards.Any(static reward => reward.Job == LegacyBossHarmRankPackageJob.Pirate);
}

public static class LegacyBossHarmRankPackagePolicy
{
    public const uint UniversalTokenItemId = 3_324_226;
    public const uint UniversalStackAItemId = 4_050_001;
    public const uint UniversalStackBItemId = 4_040_001;
    public const uint UniversalPackItemId = 3_009_003;
    public const uint WarriorItemId = 3_333_403;
    public const uint ArcherItemId = 3_335_128;
    public const uint TrojanFirstItemId = 3_321_098;
    public const uint TrojanSecondItemId = 3_321_107;
    public const uint RankTopTailFirstItemId = 3_316_793;
    public const uint RankTopTailSecondItemId = 3_330_770;

    public static bool TryGetPackage(int rank, out LegacyBossHarmRankPackageReward reward)
    {
        reward = rank switch
        {
            1 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 200,
                packQuantity: 5,
                classExperience: 1_000,
                warriorQuantity: 5,
                ninjaPoints: 50,
                archerQuantity: 10,
                trojanQuantity: 200,
                piratePoints: 50,
                tailQuantity: 10),
            2 => CreatePackage(
                rank,
                tokenQuantity: 4,
                stackQuantity: 150,
                packQuantity: 4,
                classExperience: 800,
                warriorQuantity: 4,
                ninjaPoints: 40,
                archerQuantity: 8,
                trojanQuantity: 180,
                piratePoints: 50,
                tailQuantity: 10),
            3 => CreatePackage(
                rank,
                tokenQuantity: 3,
                stackQuantity: 150,
                packQuantity: 3,
                classExperience: 700,
                warriorQuantity: 3,
                ninjaPoints: 30,
                archerQuantity: 3,
                trojanQuantity: 150,
                piratePoints: 50,
                tailQuantity: 5),
            4 => CreatePackage(
                rank,
                tokenQuantity: 2,
                stackQuantity: 100,
                packQuantity: 2,
                classExperience: 500,
                warriorQuantity: 2,
                ninjaPoints: 20,
                archerQuantity: 5,
                trojanQuantity: 100,
                piratePoints: 20,
                tailQuantity: 0),
            5 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 80,
                packQuantity: 1,
                classExperience: 1_000,
                warriorQuantity: 1,
                ninjaPoints: 10,
                archerQuantity: 1,
                trojanQuantity: 50,
                piratePoints: 20,
                tailQuantity: 0),
            6 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 50,
                packQuantity: 1,
                classExperience: 100,
                warriorQuantity: 1,
                ninjaPoints: 5,
                archerQuantity: 1,
                trojanQuantity: 50,
                piratePoints: 20,
                tailQuantity: 0),
            7 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 50,
                packQuantity: 1,
                classExperience: 100,
                warriorQuantity: 1,
                ninjaPoints: 5,
                archerQuantity: 1,
                trojanQuantity: 50,
                piratePoints: 20,
                tailQuantity: 0),
            8 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 50,
                packQuantity: 1,
                classExperience: 100,
                warriorQuantity: 1,
                ninjaPoints: 5,
                archerQuantity: 1,
                trojanQuantity: 50,
                piratePoints: 0,
                tailQuantity: 0),
            9 => CreatePackage(
                rank,
                tokenQuantity: 1,
                stackQuantity: 50,
                packQuantity: 1,
                classExperience: 100,
                warriorQuantity: 1,
                ninjaPoints: 5,
                archerQuantity: 1,
                trojanQuantity: 50,
                piratePoints: 0,
                tailQuantity: 0),
            _ => default
        };

        return reward.Rank != 0;
    }

    public static LegacyBossHarmRankPackageJob GetJobFromClassCode(long classCode)
    {
        return classCode switch
        {
            >= 1_000 and <= 1_051 => LegacyBossHarmRankPackageJob.Trojan,
            >= 2_000 and <= 2_051 => LegacyBossHarmRankPackageJob.Warrior,
            >= 4_000 and <= 4_051 => LegacyBossHarmRankPackageJob.Archer,
            >= 5_000 and <= 5_051 => LegacyBossHarmRankPackageJob.Ninja,
            >= 7_000 and <= 7_051 => LegacyBossHarmRankPackageJob.Pirate,
            _ => LegacyBossHarmRankPackageJob.Other
        };
    }

    public static bool TryGetJobReward(
        LegacyBossHarmRankPackageReward reward,
        LegacyBossHarmRankPackageJob job,
        out LegacyBossHarmRankPackageJobReward jobReward)
    {
        foreach (var candidate in reward.JobRewards)
        {
            if (candidate.Job == job)
            {
                jobReward = candidate;
                return true;
            }
        }

        jobReward = default;
        return false;
    }

    private static LegacyBossHarmRankPackageReward CreatePackage(
        int rank,
        uint tokenQuantity,
        uint stackQuantity,
        uint packQuantity,
        uint classExperience,
        uint warriorQuantity,
        uint ninjaPoints,
        uint archerQuantity,
        uint trojanQuantity,
        uint piratePoints,
        uint tailQuantity)
    {
        var items = tailQuantity == 0
            ? new[]
            {
                Item(UniversalTokenItemId, tokenQuantity),
                Item(UniversalStackAItemId, stackQuantity),
                Item(UniversalStackBItemId, stackQuantity),
                Item(UniversalPackItemId, packQuantity)
            }
            : new[]
            {
                Item(UniversalTokenItemId, tokenQuantity),
                Item(UniversalStackAItemId, stackQuantity),
                Item(UniversalStackBItemId, stackQuantity),
                Item(UniversalPackItemId, packQuantity),
                Item(RankTopTailFirstItemId, tailQuantity),
                Item(RankTopTailSecondItemId, tailQuantity)
            };

        var jobRewards = piratePoints == 0
            ? new[]
            {
                Warrior(warriorQuantity),
                Ninja(ninjaPoints),
                Archer(archerQuantity),
                Trojan(trojanQuantity)
            }
            : new[]
            {
                Warrior(warriorQuantity),
                Ninja(ninjaPoints),
                Archer(archerQuantity),
                Trojan(trojanQuantity),
                Pirate(piratePoints)
            };

        return new LegacyBossHarmRankPackageReward(rank, items, classExperience, jobRewards);
    }

    private static LegacyBossHarmRankPackageItem Item(uint itemId, uint quantity)
    {
        return new LegacyBossHarmRankPackageItem(
            itemId,
            Plus: 0,
            Quantity: quantity,
            Bound: false,
            SocketProgress: 0);
    }

    private static LegacyBossHarmRankPackageJobReward Warrior(uint quantity)
    {
        return new LegacyBossHarmRankPackageJobReward(
            LegacyBossHarmRankPackageJob.Warrior,
            [Item(WarriorItemId, quantity)],
            NinjaPoints: 0,
            PirateAirPowerPoints: 0,
            PirateExperience: 0);
    }

    private static LegacyBossHarmRankPackageJobReward Ninja(uint points)
    {
        return new LegacyBossHarmRankPackageJobReward(
            LegacyBossHarmRankPackageJob.Ninja,
            [],
            NinjaPoints: points,
            PirateAirPowerPoints: 0,
            PirateExperience: 0);
    }

    private static LegacyBossHarmRankPackageJobReward Archer(uint quantity)
    {
        return new LegacyBossHarmRankPackageJobReward(
            LegacyBossHarmRankPackageJob.Archer,
            [Item(ArcherItemId, quantity)],
            NinjaPoints: 0,
            PirateAirPowerPoints: 0,
            PirateExperience: 0);
    }

    private static LegacyBossHarmRankPackageJobReward Trojan(uint quantity)
    {
        return new LegacyBossHarmRankPackageJobReward(
            LegacyBossHarmRankPackageJob.Trojan,
            [Item(TrojanFirstItemId, quantity), Item(TrojanSecondItemId, quantity)],
            NinjaPoints: 0,
            PirateAirPowerPoints: 0,
            PirateExperience: 0);
    }

    private static LegacyBossHarmRankPackageJobReward Pirate(uint points)
    {
        return new LegacyBossHarmRankPackageJobReward(
            LegacyBossHarmRankPackageJob.Pirate,
            [],
            NinjaPoints: 0,
            PirateAirPowerPoints: points,
            PirateExperience: points);
    }
}
