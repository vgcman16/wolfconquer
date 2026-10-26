namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerVipAsync(
        WorldPacketHandlerContext context, WorldSession session, byte optionId, ulong vipLevel,
        ulong cost, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccountVipOwner owner = null!;
        AccountVipPlan plan = null!;
        var insufficient = false;
        var committed = false;
        var outcome = LegacyAccountManagerVipPersistence.Outcome.Rejected;
        var reason = "accountManagerVipOwnerUnavailable";
        if (context.Trades is { } trades)
        {
            // Existing actual order: party gate -> Store connection gate ->
            // Character image gate. The Frame/registry owns its real world lease.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryCaptureAccountVipOwner(context, session, out owner, out reason) &&
                        TryPrepareAccountVip(context, owner, optionId, vipLevel, cost, out plan, out insufficient, out reason))
                    {
                        reason = "accountManagerVipCurrentSessionChanged";
                        committed = context.Sessions!.TryCommitSession(context.Connection, session, plan.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsCurrentAccountVipOwner(context, owner, session, owner.Fields)) return false;
                            outcome = LegacyAccountManagerVipPersistence.Commit(plan.File, out reason);
                            return outcome == LegacyAccountManagerVipPersistence.Outcome.Committed;
                        });
                    }
                }
                catch (Exception error) when (LegacyAccountManagerVipPersistence.IsFailure(error))
                {
                    reason = (outcome == LegacyAccountManagerVipPersistence.Outcome.Committed
                        ? LegacyAccountManagerVipPersistence.RecoveryRequired + ":sessionPublicationUnproven:"
                        : "accountManagerVipPreparation:") + error.GetType().Name;
                }
                if (!committed && (outcome != LegacyAccountManagerVipPersistence.Outcome.Rejected ||
                    reason.StartsWith(LegacyAccountManagerVipPersistence.RecoveryRequired, StringComparison.Ordinal)))
                    context.TradeRecovery?.Block(LegacyAccountManagerVipPersistence.RecoveryRequired + ":" + reason);
            }
        }
        if (committed)
        {
            await PublishAccountVipAsync(context, plan, cancellationToken).ConfigureAwait(false);
            return true;
        }
        if (insufficient && owner is not null)
            await PublishAccountVipInsufficientAsync(context, owner, cost, cancellationToken).ConfigureAwait(false);
        var status = outcome != LegacyAccountManagerVipPersistence.Outcome.Rejected ||
            reason.StartsWith(LegacyAccountManagerVipPersistence.RecoveryRequired, StringComparison.Ordinal)
            ? "held for recovery" : "skipped";
        await context.Log.WriteLineAsync($"WorldServer {status} LegacyNpc AccountManager VIP purchase " +
            $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} reason={reason}.").ConfigureAwait(false);
        return true;
    }
}
