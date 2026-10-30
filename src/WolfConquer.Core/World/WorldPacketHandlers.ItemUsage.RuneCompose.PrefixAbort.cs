using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask CommitLegacyRuneComposeAcceptedPrefixAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem originalTarget,
        PlayerItemSnapshotItem updatedTarget,
        PlayerItemSnapshot updatedItems,
        IReadOnlyList<byte[]> mutationFrames,
        CancellationToken cancellationToken)
    {
        var prefixItems = ReplaceRuneItem(updatedItems, originalTarget, updatedTarget);
        var prefixSession = session with { PlayerItems = prefixItems };
        prefixSession = PublishLegacyItemUsageSession(context, prefixSession);
        var prefixContext = context with { Session = prefixSession };

        // Compatibility persistence of the accepted prefix, not native Save/SQL timing.
        await TryPersistLegacyItemUsageMutationAsync(
                prefixContext,
                prefixSession,
                "compose rune accepted prefix",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }
    }
}
