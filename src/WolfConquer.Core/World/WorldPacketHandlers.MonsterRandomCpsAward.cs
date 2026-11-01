using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class MonsterRandomCpsAward { }
    private sealed record MonsterRandomCpsAwardData(WorldPacketHandlerContext Context, WorldSession Before,
        LegacyMonsterInstance Monster, int FamilyId, uint Amount);
    private static readonly ConditionalWeakTable<MonsterRandomCpsAward, MonsterRandomCpsAwardData> monsterRandomCpsAwards = new();

    // The actual random monster-death branch is the sole issuer.
    private static MonsterRandomCpsAward IssueMonsterRandomCpsAward(WorldPacketHandlerContext context,
        WorldSession before, LegacyMonsterInstance monster, int familyId, uint amount)
    {
        var token = new MonsterRandomCpsAward();
        monsterRandomCpsAwards.Add(token, new(context, before, monster, familyId, amount));
        return token;
    }

    internal static bool ConsumeMonsterRandomCpsAward(MonsterRandomCpsAward token, WorldSessionStore store,
        WorldPacketHandlerContext context, WorldSession before, out uint amount)
    {
        amount = 0;
        if (!monsterRandomCpsAwards.TryGetValue(token, out var data) || !ReferenceEquals(data.Context, context) ||
            !ReferenceEquals(data.Context.Sessions, store) || !ReferenceEquals(data.Before, before) ||
            data.Monster.MonsterId != data.FamilyId || data.Amount == 0 || !monsterRandomCpsAwards.Remove(token)) return false;
        amount = data.Amount; return true;
    }
}
