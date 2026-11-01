using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class MonsterBossCpsAward { }
    private sealed record MonsterBossCpsAwardData(WorldPacketHandlerContext Context, WorldSession Before,
        LegacyMonsterInstance Monster, int FamilyId, LegacyBossDeathConquerPointReward Reward);
    private static readonly ConditionalWeakTable<MonsterBossCpsAward, MonsterBossCpsAwardData> monsterBossCpsAwards = new();

    // Only the actual boss-death policy branch issues this typed capability.
    private static MonsterBossCpsAward IssueMonsterBossCpsAward(WorldPacketHandlerContext context,
        WorldSession before, LegacyMonsterInstance monster, int familyId, LegacyBossDeathConquerPointReward reward)
    {
        var token = new MonsterBossCpsAward();
        monsterBossCpsAwards.Add(token, new(context, before, monster, familyId, reward));
        return token;
    }

    internal static bool ConsumeMonsterBossCpsAward(MonsterBossCpsAward token, WorldSessionStore store,
        WorldPacketHandlerContext context, WorldSession before, out uint amount)
    {
        amount = 0;
        if (!monsterBossCpsAwards.TryGetValue(token, out var data) || !ReferenceEquals(data.Context, context) ||
            !ReferenceEquals(data.Context.Sessions, store) || !ReferenceEquals(data.Before, before) ||
            data.FamilyId < 0 || data.Monster.MapId < 0 || data.Monster.MonsterId != data.FamilyId ||
            !LegacyBossDeathRewardPolicy.TryGetConquerPointReward(checked((uint)data.FamilyId), checked((uint)data.Monster.MapId),
                data.Monster.Boss, out var current) || current != data.Reward ||
            data.Reward.TotalConquerPoints is <= 0 or > uint.MaxValue || !monsterBossCpsAwards.Remove(token)) return false;
        amount = checked((uint)data.Reward.TotalConquerPoints); return true;
    }
}
