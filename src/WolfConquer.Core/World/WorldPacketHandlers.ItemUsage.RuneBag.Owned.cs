namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleOwnedDirectRuneBagAsync(WorldPacketHandlerContext context,
        WorldSession session, MsgItemUsagePacket packet, byte[] echo, CancellationToken cancellationToken)
    {
        bool intoBag = packet.DwParam == 0;
        if (context.Trades is not { } trades)
        {
            await context.Log.WriteLineAsync("WorldServer held direct rune bag reason=ownerUnavailable; no success was published.")
                .ConfigureAwait(false);
            return;
        }
        WorldSession updated; byte[][] frames; object identity; string reason; bool committed;
        // Keep the one issued namespace's final deny-only fence under the
        // actual producer lock. No subsequent request can reserve in the gap
        // between an uncommitted return and its recovery fence. No await here.
        lock (trades.SyncRoot)
        {
            LegacyRuneBagEvidence? issued = null;
            try
            {
                committed = TryCommitLegacyItemSpell(context, session, (stable, _, before) =>
                {
                    // Projection is repeated by the actual writer. Reuse this
                    // invocation's capability and its one real allocation.
                    issued ??= LegacyRuneBagEvidence.Capture(context, stable, packet.Id, intoBag, before);
                    if (!issued.IsCurrent()) throw InvalidSpellMutation();
                    return PrepareOwnedRuneBagProjection(stable, issued, echo);
                }, cancellationToken, out updated, out frames, out identity, out reason);
            }
            finally { issued?.FenceUnlessCommitted(); }
        }
        if (!committed)
        {
            await context.Log.WriteLineAsync($"WorldServer held direct rune bag itemUid={packet.Id} reason={reason}; no success was published.")
                .ConfigureAwait(false);
            return;
        }

        await PublishItemSpellFramesAsync(context, updated, frames, identity, cancellationToken).ConfigureAwait(false);
        if (trades.HasConnectionEnded(context.Connection) || context.Sessions is not { } store || !LegacyNativeClientPublication.TryCurrent(store, context.Connection,
                updated.Character.Uid, identity, out var current) || !ReferenceEquals(current, updated)) return;
        var ranked = updated;
        if (intoBag)
        {
            // This is a separate source-ordered post-commit query, not a RuneBag
            // WAL afterimage, a precommit HP clamp or a replayable notification.
            var query = await ExecuteLegacyEquipmentQueryAsync(context with { Session = updated }, updated,
                new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.RuneStorageQuery,
                    new LegacyEquipmentQueryPublicationScope(updated.Character.Uid, updated.PlayerItems)
                        { OwnerNativeClientIdentity = identity }), cancellationToken).ConfigureAwait(false);
            if (!query.Completed || trades.HasConnectionEnded(context.Connection)) return;
            ranked = query.Session!;
        }
        UpdateLegacyRuneRanking(context with { Session = ranked }, ranked);
        await context.Log.WriteLineAsync($"WorldServer committed direct rune bag {(intoBag ? "add" : "remove")} " +
            $"itemUid={packet.Id} source={LegacyDirectRuneBagSource} sourceLines={LegacyDirectRuneBagLines}.").ConfigureAwait(false);
    }
}
