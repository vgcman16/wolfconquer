namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerAttributeFlagAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, ulong cost, string attributeName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountChiOwner? owner = null;
        AccountChiPlan? plan = null;
        var insufficient = false;
        var committed = false;
        var outcome = LegacyAccountManagerChargeChiPersistence.Outcome.Rejected;
        var reason = "accountManagerChargeChiOwnerUnavailable";
        if (context.Trades is { } trades && context.Sessions is { } sessions)
        {
            // Same real order as VIP: party -> Store connection -> Character image.
            // Frame dispatch already owns the actual world-operation lease.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryCaptureAccountChiOwner(context, session, out owner, out reason) && owner is not null &&
                        TryPrepareAccountChi(context, owner, optionId, cost, attributeName,
                            out plan, out insufficient, out reason) && plan is not null)
                    {
                        var prepared = plan;
                        reason = "accountManagerChargeChiCurrentSessionChanged";
                        committed = sessions.TryCommitSession(context.Connection, session, prepared.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsCurrentAccountChiOwner(context, prepared.Owner, session, prepared.Owner.Fields)) return false;
                            outcome = LegacyAccountManagerChargeChiPersistence.Commit(prepared.File,
                                () =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    return IsCurrentAccountChiOwner(context, prepared.Owner, session, prepared.Owner.Fields);
                                }, out reason);
                            return outcome == LegacyAccountManagerChargeChiPersistence.Outcome.Committed;
                        });
                    }
                }
                catch (Exception error) when (LegacyAccountManagerChargeChiPersistence.IsFailure(error))
                {
                    reason = (outcome == LegacyAccountManagerChargeChiPersistence.Outcome.Committed
                        ? LegacyAccountManagerChargeChiPersistence.RecoveryRequired + ":sessionPublicationUnproven:"
                        : "accountManagerChargeChiPreparation:") + error.GetType().Name;
                }
                if (!committed && (outcome != LegacyAccountManagerChargeChiPersistence.Outcome.Rejected ||
                    reason.StartsWith(LegacyAccountManagerChargeChiPersistence.RecoveryRequired, StringComparison.Ordinal)))
                    context.TradeRecovery?.Block(LegacyAccountManagerChargeChiPersistence.RecoveryRequired + ":" + reason);
            }
        }
        if (committed && plan is not null)
        {
            await PublishAccountChiAsync(context, plan).ConfigureAwait(false);
            return true;
        }
        if (insufficient && owner is not null)
            await PublishAccountChiInsufficientAsync(context, owner, cancellationToken).ConfigureAwait(false);
        var status = outcome != LegacyAccountManagerChargeChiPersistence.Outcome.Rejected ||
            reason.StartsWith(LegacyAccountManagerChargeChiPersistence.RecoveryRequired, StringComparison.Ordinal)
            ? "held for recovery" : "skipped";
        await context.Log.WriteLineAsync($"WorldServer {status} LegacyNpc AccountManager attribute flag purchase " +
            $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} reason={reason}.").ConfigureAwait(false);
        return true;
    }
}
