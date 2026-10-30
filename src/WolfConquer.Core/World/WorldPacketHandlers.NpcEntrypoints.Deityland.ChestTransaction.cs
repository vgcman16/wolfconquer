namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryUnlockLegacyDeitylandChestAsync(WorldPacketHandlerContext context,
        WorldSession session, string sourceMethod, uint keyItemId, uint rewardItemId,
        uint conquerPointsReward, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeityChestOwner owner = null!;
        DeityChestPlan plan = null!;
        byte[]? refusal = null;
        var reason = "deityChestTradeOwnerUnavailable";
        var committed = false;
        WorldSession? final = null;
        var journalReturnedSuccess = false;
        if (context.Trades is { } trades)
        {
            // Same order as Trade: actual party gate -> Store connection gate ->
            // ordered snapshot locks. The Frame already owns its shared lease;
            // never attempt a shared-to-exclusive barrier upgrade here.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryCaptureDeityChestOwner(context, session, out owner, out reason) &&
                        TryPrepareDeityChest(context, owner, sourceMethod, keyItemId, rewardItemId, conquerPointsReward,
                            out plan, out refusal, out reason))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        reason = "deityChestCurrentOwnerChanged";
                        committed = context.Sessions!.TryCommitSession(context.Connection, session, plan.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsPreparedDeityChestOwner(context, owner, plan)) return false;
                            var saved = LegacyDeityChestPersistence.TryPersist(plan.Files, plan.Updated, plan.Items, out reason);
                            journalReturnedSuccess = saved;
                            return saved;
                        }, out final);
                    }
                }
                catch (Exception error) when (LegacyDeityChestPersistence.IsPreparationFailure(error))
                {
                    reason = (journalReturnedSuccess ? LegacyDeityChestPersistence.RecoveryRequired + ":memoryPublicationUnproven:" :
                        "deityChestPreparation:") + error.GetType().Name;
                }
                if (!committed && reason.StartsWith(LegacyDeityChestPersistence.RecoveryRequired, StringComparison.Ordinal))
                    context.TradeRecovery?.Block(reason);
            }
        }
        if (committed)
        {
            await PublishDeityChestAsync(context, owner!, plan!, final!, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (refusal is not null && owner is not null && IsCurrentDeityChestOwner(context, owner, session) &&
            DeityChestAttributesCurrent(owner) && owner.Items.IsCurrent(session.PlayerItems))
            await context.Connection.SendAsync(refusal, cancellationToken).ConfigureAwait(false);
        var prefix = "WorldServer skipped LegacyNpc Deityland chest unlock " + $"{FormatSession(context)} source={sourceMethod} ";
        var diagnostic = reason switch
        {
            "key missing" => prefix + $"keyItemId={keyItemId}; key missing.",
            "inventory is full" => prefix + $"rewardItemId={rewardItemId}; inventory is full.",
            _ => prefix + $"keyItemId={keyItemId} rewardItemId={rewardItemId}; " +
                (reason.StartsWith(LegacyDeityChestPersistence.RecoveryRequired, StringComparison.Ordinal)
                    ? "indeterminate; recovery fenced; " : "rejected without commit; ") + $"reason={reason}.",
        };
        await context.Log.WriteLineAsync(diagnostic).ConfigureAwait(false);
    }
}
