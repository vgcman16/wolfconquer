namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Both frames belong to the actual committed Store output, and each real
    // registered write must complete before the next may start.
    private static async ValueTask PublishOwnedRelicWireAsync(WorldPacketHandlerContext context,
        WorldSessionStore.RelicWireOutput output, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } store) throw new LegacyCommittedSpellPublicationException();
        try
        {
            if (!store.TryHandoffRelicOutput(output))
                throw new LegacyCommittedSpellPublicationException(new IOException("relicCommittedHandoffUnproven"));
            for (var index = 0; index < 2; index++)
            {
                if (!store.TryIssueRelicWire(output, out var permit) || permit is null ||
                    !await context.Connection.SendOwnedRelicAsync(store, permit, cancellationToken).ConfigureAwait(false) ||
                    !store.CompleteRelicWire(output, permit))
                    throw new LegacyCommittedSpellPublicationException(new IOException("relicCommittedOutputUnproven"));
            }
        }
        catch (Exception failure)
        {
            // A timed-out or partially encrypted stream is never reused.
            try { await context.Connection.DisconnectAsync().ConfigureAwait(false); }
            catch (Exception closeFailure)
            { throw new LegacyCommittedSpellPublicationException(new AggregateException(failure, closeFailure)); }
            if (failure is LegacyCommittedSpellPublicationException) throw;
            // Cancellation after commitment is an output failure. Preserve its
            // cause without letting dispatch treat it as an uncommitted cancel.
            throw new LegacyCommittedSpellPublicationException(failure);
        }
    }
}
