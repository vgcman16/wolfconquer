namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession?> PublishLegacyRootAcceptedCostsAsync(
        WorldPacketHandlerContext context, LegacyRootAdmissionOwner owner,
        WorldSession session, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
    {
        foreach (var frame in frames)
        {
            if (!RefreshLegacyRootAcceptedOwner(context, owner, ref session)) return null;
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            if (!RefreshLegacyRootAcceptedOwner(context, owner, ref session)) return null;
        }
        return RefreshLegacyRootAcceptedOwner(context, owner, ref session) ? session : null;
    }
}
