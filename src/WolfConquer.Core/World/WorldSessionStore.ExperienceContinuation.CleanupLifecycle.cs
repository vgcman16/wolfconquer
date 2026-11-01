namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Retirement-only. A registered transport close can end gameplay before
    // its queued cleanup obtains the exclusive operation still held here.
    // The Frame's closed-lifetime proof does not replace the checkpoint's
    // independent exact holder/native/file/journal checks.
    private bool IsContinuationCleanupLifecycleCurrent(WorldServerFrameHandler frame,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding) =>
        ReferenceEquals(framePersistenceOwner, frame) && ReferenceEquals(binding.Store, this) &&
        ReferenceEquals(binding.Connection, context.Connection) &&
        Monitor.IsEntered(frame.RequireActionLifecycleGate(this)) &&
        context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true &&
        (frame.IsActionLifecycleCurrent(this, context) ||
            frame.IsEndedContinuationCleanupCurrent(this, context, binding));
}
