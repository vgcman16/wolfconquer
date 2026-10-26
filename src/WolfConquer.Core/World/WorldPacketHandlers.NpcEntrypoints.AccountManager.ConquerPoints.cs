namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerConquerPointsAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, ulong cost, long conquerPointsGrant,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountCpOwner? owner = null;
        AccountCpPlan? plan = null;
        var insufficient = false;
        var committed = false;
        var outcome = LegacyAccountManagerConquerPointsPersistence.Outcome.Rejected;
        var reason = "accountManagerConquerPointsOwnerUnavailable";
        if (context.Trades is { } trades && context.Sessions is { } sessions)
        {
            // Same real order as VIP: party -> Store connection -> Character image.
            // Frame dispatch already owns the actual world-operation lease.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryCaptureAccountCpOwner(context, session, out owner, out reason) && owner is not null &&
                        TryPrepareAccountCp(context, owner, optionId, cost, conquerPointsGrant,
                            out plan, out insufficient, out reason) && plan is not null)
                    {
                        var prepared = plan;
                        reason = "accountManagerConquerPointsCurrentSessionChanged";
                        committed = sessions.TryCommitSession(context.Connection, session, prepared.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsCurrentAccountCpOwner(context, prepared.Owner, session, prepared.Owner.Fields)) return false;
                            outcome = LegacyAccountManagerConquerPointsPersistence.Commit(prepared.File,
                                () =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    return IsCurrentAccountCpOwner(context, prepared.Owner, session, prepared.Owner.Fields);
                                }, out reason);
                            return outcome == LegacyAccountManagerConquerPointsPersistence.Outcome.Committed;
                        });
                    }
                }
                catch (Exception error) when (LegacyAccountManagerConquerPointsPersistence.IsFailure(error))
                {
                    reason = (outcome == LegacyAccountManagerConquerPointsPersistence.Outcome.Committed
                        ? LegacyAccountManagerConquerPointsPersistence.RecoveryRequired + ":sessionPublicationUnproven:"
                        : "accountManagerConquerPointsPreparation:") + error.GetType().Name;
                }
                if (!committed && (outcome != LegacyAccountManagerConquerPointsPersistence.Outcome.Rejected ||
                    reason.StartsWith(LegacyAccountManagerConquerPointsPersistence.RecoveryRequired, StringComparison.Ordinal)))
                    context.TradeRecovery?.Block(LegacyAccountManagerConquerPointsPersistence.RecoveryRequired + ":" + reason);
            }
        }
        if (committed && plan is not null)
        {
            await PublishAccountCpAsync(context, plan, cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (insufficient && owner is not null)
            await PublishAccountCpInsufficientAsync(context, owner, cancellationToken).ConfigureAwait(false);
        var status = outcome != LegacyAccountManagerConquerPointsPersistence.Outcome.Rejected ||
            reason.StartsWith(LegacyAccountManagerConquerPointsPersistence.RecoveryRequired, StringComparison.Ordinal)
            ? "held for recovery" : "skipped";
        await context.Log.WriteLineAsync($"WorldServer {status} LegacyNpc AccountManager conquer point purchase " +
            $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} reason={reason}.").ConfigureAwait(false);
        return true;
    }
}
