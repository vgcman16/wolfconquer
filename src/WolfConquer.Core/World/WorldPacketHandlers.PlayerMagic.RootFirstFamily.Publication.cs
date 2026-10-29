namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> PublishLegacyRootAcceptedCostsAsync(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner,
        WorldSession session, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
    {
        if (owner.CostOutput is { } output)
        {
            var current = await output.SendAsync(frames, cancellationToken).ConfigureAwait(false);
            if (current is null)
                await context.Log.WriteLineAsync("WorldServer interrupted committed magic cost output; " +
                    "accepted accounting retained, no refund or retry authorization.").ConfigureAwait(false);
            return current;
        }
        // Existing no-Store local-record compatibility has no live receipt.
        foreach (var frame in frames)
        {
            if (!RefreshLegacyRootAcceptedOwner(context, owner, ref session)) return null;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!RefreshLegacyRootAcceptedOwner(context, owner, ref session)) return null;
        }
        return RefreshLegacyRootAcceptedOwner(context, owner, ref session) ? session : null;
    }
}
