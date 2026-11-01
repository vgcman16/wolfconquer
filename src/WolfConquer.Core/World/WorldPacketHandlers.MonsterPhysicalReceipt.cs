using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class CompletedMonsterPhysicalHit { }
    private sealed record CompletedMonsterPhysicalHitData(WorldPacketHandlerContext Context,
        WorldServerFrameHandler.MonsterPhysicalAttempt Attempt, WorldSession Final);
    private static readonly ConditionalWeakTable<CompletedMonsterPhysicalHit, CompletedMonsterPhysicalHitData> completedMonsterPhysicalHits = new();

    // Called by the actual monster Frame path only after the hit, XP outcome,
    // query and required output have all returned successfully.
    private static CompletedMonsterPhysicalHit IssueCompletedMonsterPhysicalHit(WorldPacketHandlerContext context,
        WorldServerFrameHandler.MonsterPhysicalAttempt attempt, WorldSession final)
    {
        var receipt = new CompletedMonsterPhysicalHit();
        completedMonsterPhysicalHits.Add(receipt, new(context, attempt, final));
        return receipt;
    }

    internal static bool TryReadCompletedMonsterPhysicalHit(CompletedMonsterPhysicalHit receipt,
        WorldSessionStore store, out WorldPacketHandlerContext context,
        out WorldServerFrameHandler.MonsterPhysicalAttempt attempt, out WorldSession final)
    {
        context = null!; attempt = null!; final = null!;
        if (!completedMonsterPhysicalHits.TryGetValue(receipt, out var data) ||
            !ReferenceEquals(data.Context.Sessions, store)) return false;
        if (!completedMonsterPhysicalHits.Remove(receipt)) return false;
        context = data.Context; attempt = data.Attempt; final = data.Final; return true;
    }
}
