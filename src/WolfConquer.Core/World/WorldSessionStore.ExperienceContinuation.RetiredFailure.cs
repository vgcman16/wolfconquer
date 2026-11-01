using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private readonly ConditionalWeakTable<ContinuationRetirementCheckpoint, object> consumedContinuationFinishes = new();

    // The real monster attempt's outer catch may observe the same exception
    // after the continuation already verified every retained image and retired
    // its actor. This consumes that historical cleanup once; it neither starts
    // retirement nor treats an absent holder/exception type as recovery proof.
    private bool TryConsumeSafelyRetiredContinuationFailure(WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, Exception failure)
    {
        if (failure is null || !ReferenceEquals(binding.Store, this) || framePersistenceOwner is not { } frame ||
            !IsContinuationCleanupLifecycleCurrent(frame, context, binding) ||
            !ReferenceEquals(context.Connection, binding.Connection) || !binding.IsRetired ||
            !continuationRetirements.TryGetValue(binding, out var slot) || slot.Latest is not { } checkpoint ||
            !ReferenceEquals(checkpoint.RetiredFailure, failure) || !SameRetirementOperation(checkpoint.Context, context) ||
            !ReferenceEquals(checkpoint.Session.NativeGameClient, binding.Client) ||
            checkpoint.Session.AccountId != binding.AccountId || checkpoint.Session.Character.Uid != binding.Uid ||
            consumedContinuationFinishes.TryGetValue(checkpoint, out _)) return false;
        consumedContinuationFinishes.Add(checkpoint, new object());
        return true;
    }
}
