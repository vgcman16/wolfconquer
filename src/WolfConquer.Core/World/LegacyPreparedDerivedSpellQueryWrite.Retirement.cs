namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedDerivedSpellQueryWrite
{
    // Only this actual completed journal23 writer exposes its participant
    // images. These bytes are cleanup evidence, not a Character write permit.
    internal bool TryCaptureContinuationRetirement(WorldSessionStore actualStore, WorldPacketHandlerContext actualContext,
        LegacyOrdinarySpellBinding actualBinding, WorldSession before, WorldSession after,
        out byte[] character, out string itemPath, out byte[] items)
    {
        character = items = []; itemPath = string.Empty;
        if (pendingContinuation is null || !ReferenceEquals(store, actualStore) || !ReferenceEquals(binding, actualBinding) ||
            !ReferenceEquals(context.FrameOperationLease, actualContext.FrameOperationLease) ||
            !CoversCommittedPublication(frame, actualStore, actualBinding.Connection, before, after)) return false;
        character = characterAfter.ToArray(); itemPath = itemsPath; items = itemsBefore.ToArray();
        return true;
    }
}
