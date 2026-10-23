namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryUnlockLegacyVictoryChestAsync(WorldPacketHandlerContext context,
        WorldSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        VictoryChestView view;
        LegacyDragonSkinInventory inventory;
        var reason = string.Empty;
        try
        {
            if (!TryCaptureVictoryChestView(context, session, out view, out reason))
            { await LogVictoryChestRefusalAsync(context, reason).ConfigureAwait(false); return; }
            // Read-only dialogs require current quest authority, not a fabricated
            // allocation/save owner. Retain both old no-grant paths.
            var text = view.Claim?.Status == Data.PlayerQuestStatus.Finished ? LegacyVictoryChestAlreadyUnlockedText :
                !IsLegacyVictoryChestObjectiveReady(view.Claim) ? LegacyVictoryChestMissingKillsText : null;
            if (text is not null)
            {
                if (IsCurrentVictoryChestView(view))
                    await SendLegacyNpcDialogAsync(context, "NpcHandler.VictoryChest", [text],
                        LegacyVictoryChestSingleCloseOptions, avatarId: null, cancellationToken).ConfigureAwait(false);
                return;
            }
            if (!LegacyDragonSkinInventory.TryCapture(session.PlayerItems, out inventory, out reason))
            { await LogVictoryChestRefusalAsync(context, reason).ConfigureAwait(false); return; }
            if (!HasLegacyInventorySpace(session.PlayerItems, 1))
            {
                if (inventory.IsCurrent(session.PlayerItems) && IsCurrentVictoryChestView(view))
                    await context.Connection.SendAsync(LegacyNpcReplyPacket.ForMessageBox(
                        "Please make 1 more space in your inventory.").ToFrameBytes(), cancellationToken).ConfigureAwait(false);
                await LogVictoryChestRefusalAsync(context, "inventory is full").ConfigureAwait(false);
                return;
            }
        }
        catch (Exception error) when (LegacyVictoryChestPersistence.IsPreparationFailure(error))
        { await LogVictoryChestRefusalAsync(context, "victoryChestPreparation:" + error.GetType().Name).ConfigureAwait(false); return; }

        VictoryChestPlan plan = null!;
        var committed = false;
        var journalReturnedSuccess = false;
        reason = "victoryChestRequiredTradeOwnerUnavailable";
        if (context.Trades is { } trades)
        {
            // The genuine Frame/registered owner already owns its operation scope.
            // Same actual Trade -> Store -> ordered snapshot lock sequence; no await,
            // second barrier acquisition or Shared-to-Exclusive upgrade here.
            lock (trades.SyncRoot)
            {
                try
                {
                    if (TryPrepareVictoryChest(context, view, inventory, out plan, out reason))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        reason = "victoryChestCurrentOwnerChanged";
                        committed = context.Sessions!.TryCommitSession(context.Connection, session, plan.Updated, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!IsPreparedVictoryChest(plan)) return false;
                            var saved = LegacyVictoryChestPersistence.TryPersist(plan.Files, plan.Updated,
                                plan.Inventory, plan.Quests, out reason);
                            journalReturnedSuccess = saved;
                            return saved;
                        });
                    }
                }
                catch (Exception error) when (LegacyVictoryChestPersistence.IsPreparationFailure(error))
                {
                    reason = journalReturnedSuccess ? LegacyVictoryChestPersistence.RecoveryRequired +
                        ":memoryPublicationUnproven:" + error.GetType().Name : "victoryChestPreparation:" + error.GetType().Name;
                }
                if (!committed && reason.StartsWith(LegacyVictoryChestPersistence.RecoveryRequired, StringComparison.Ordinal))
                    context.TradeRecovery?.Block(reason);
            }
        }
        if (committed)
            await PublishVictoryChestAsync(plan, cancellationToken).ConfigureAwait(false);
        else
            await LogVictoryChestRefusalAsync(context, reason).ConfigureAwait(false);
    }

    private static ValueTask LogVictoryChestRefusalAsync(WorldPacketHandlerContext context, string reason) =>
        new(context.Log.WriteLineAsync("WorldServer skipped LegacyNpc VictoryChest unlock " +
            $"{FormatSession(context)} source=NpcHandler.VictoryChest questId={LegacyDeityLandQuestId}; " +
            (reason.StartsWith(LegacyVictoryChestPersistence.RecoveryRequired, StringComparison.Ordinal)
                ? "indeterminate; recovery fenced; " : "rejected without commit; ") + $"reason={reason}."));
}
