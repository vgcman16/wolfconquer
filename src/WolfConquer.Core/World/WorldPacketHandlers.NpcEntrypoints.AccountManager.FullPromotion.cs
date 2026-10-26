namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerFullPromotionAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountPromotionOwner? owner = null;
        AccountPromotionPlan? plan = null;
        var insufficient = false;
        var committed = false;
        var outcome = LegacyAccountManagerFullPromotionPersistence.Outcome.Rejected;
        var reason = "accountManagerFullPromotionOwnerUnavailable";
        if (context.Trades is { } trades && context.Sessions is { } sessions)
        {
            // Same real order as VIP: party -> Store connection -> Character image.
            // Frame dispatch already owns the actual world-operation lease.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryCaptureAccountPromotionOwner(context, session, out owner, out reason) && owner is not null &&
                        TryPrepareAccountPromotion(context, owner, optionId, out plan, out insufficient, out reason) && plan is not null)
                    {
                        var prepared = plan;
                        reason = "accountManagerFullPromotionCurrentSessionChanged";
                        committed = sessions.TryCommitSession(context.Connection, session, prepared.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsCurrentAccountPromotionOwner(context, prepared.Owner, session, prepared.Owner.Fields)) return false;
                            outcome = LegacyAccountManagerFullPromotionPersistence.Commit(prepared.File,
                                () =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    return IsCurrentAccountPromotionOwner(context, prepared.Owner, session, prepared.Owner.Fields);
                                }, out reason);
                            return outcome == LegacyAccountManagerFullPromotionPersistence.Outcome.Committed;
                        });
                    }
                }
                catch (Exception error) when (LegacyAccountManagerFullPromotionPersistence.IsFailure(error))
                {
                    reason = (outcome == LegacyAccountManagerFullPromotionPersistence.Outcome.Committed
                        ? LegacyAccountManagerFullPromotionPersistence.RecoveryRequired + ":sessionPublicationUnproven:"
                        : "accountManagerFullPromotionPreparation:") + error.GetType().Name;
                }
                if (!committed && (outcome != LegacyAccountManagerFullPromotionPersistence.Outcome.Rejected ||
                    reason.StartsWith(LegacyAccountManagerFullPromotionPersistence.RecoveryRequired, StringComparison.Ordinal)))
                    context.TradeRecovery?.Block(LegacyAccountManagerFullPromotionPersistence.RecoveryRequired + ":" + reason);
            }
        }
        if (committed && plan is not null)
        {
            await PublishAccountPromotionAsync(context, plan).ConfigureAwait(false);
            return true;
        }
        if (insufficient && owner is not null)
            await PublishAccountPromotionInsufficientAsync(context, owner, cancellationToken).ConfigureAwait(false);
        var status = outcome != LegacyAccountManagerFullPromotionPersistence.Outcome.Rejected ||
            reason.StartsWith(LegacyAccountManagerFullPromotionPersistence.RecoveryRequired, StringComparison.Ordinal)
            ? "held for recovery" : "skipped";
        await context.Log.WriteLineAsync($"WorldServer {status} LegacyNpc AccountManager full promotion " +
            $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} reason={reason}.").ConfigureAwait(false);
        return true;
    }
}
