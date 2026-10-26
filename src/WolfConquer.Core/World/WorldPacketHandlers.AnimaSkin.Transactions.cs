namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ValueTask HandleLegacyDragonSkinUnlockAsync(WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet, WorldSession session, CancellationToken cancellationToken) =>
        HandleLegacyDragonSkinMutationAsync(context, packet, session, cancellationToken);

    private static ValueTask HandleLegacyDragonSkinRetrieveAsync(WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet, WorldSession session, CancellationToken cancellationToken) =>
        HandleLegacyDragonSkinMutationAsync(context, packet, session, cancellationToken);

    private static async ValueTask HandleLegacyDragonSkinMutationAsync(WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet, WorldSession session, CancellationToken cancellationToken)
    {
        if (!TryCaptureDragonSkinOwner(context, session, out var owner, out var reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        if (context.TradeOperations is not { } operations || context.TradeRecovery is not { } recovery ||
            !recovery.IsReadyFor(session.UsersRoot, context.PlayerItemsRoot))
        { await LogDragonSkinRefusalAsync(context, "dragonSkinRecoveryOrOperationOwnerUnavailable").ConfigureAwait(false); return; }
        DragonSkinMutationPlan? committed = null;
        byte[]? rejection = null;
        using (await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!recovery.IsReadyFor(session.UsersRoot, context.PlayerItemsRoot)) reason = "dragonSkinRecoveryRequired";
            else if (context.Trades?.HasConnectionEnded(context.Connection) == true) reason = "dragonSkinConnectionEnded";
            else if (!IsCurrentDragonSkinOwner(context, owner, session) || !owner.Book.IsCurrent(session.Character.Attributes))
                reason = "dragonSkinCurrentOwnerChanged";
            else if (TryPrepareDragonSkinMutation(context, owner, packet, out var plan, out rejection, out reason))
            {
                var failure = "dragonSkinCurrentOwnerChanged";
                var saved = context.Sessions!.TryCommitSession(context.Connection, session, plan!.Updated, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!recovery.IsReadyFor(session.UsersRoot, context.PlayerItemsRoot))
                    { failure = "dragonSkinRecoveryRequired"; return false; }
                    if (!owner.Book.IsCurrent(session.Character.Attributes) || !plan.BeforeItems.IsCurrent(session.PlayerItems) ||
                        !plan.AfterItems.IsCurrent(plan.Updated.PlayerItems) || !IsCurrentDragonSkinOwner(context, owner, session))
                    { failure = "dragonSkinPreparedOwnerChanged"; return false; }
                    return LegacyDragonSkinPersistence.TryPersist(session, plan.Updated, context.PlayerItemsRoot, out failure);
                });
                if (saved) { committed = plan; reason = "committed"; }
                else
                {
                    reason = failure;
                    if (failure.StartsWith(LegacyDragonSkinPersistence.RecoveryRequired, StringComparison.Ordinal)) recovery.Block(failure);
                }
            }
        }
        if (committed is not null)
        {
            await PublishDragonSkinMutationAsync(context, owner, committed, packet, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (IsOrdinaryDragonSkinRefusal(reason) && IsCurrentDragonSkinOwner(context, owner, session))
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyDragonSkin {packet.Type} {FormatSession(context)} " +
                $"skinType={packet.SkinType}; {reason}.").ConfigureAwait(false);
            if (rejection is not null && IsCurrentDragonSkinOwner(context, owner, session))
                await context.Connection.SendAsync(rejection, cancellationToken).ConfigureAwait(false);
            return;
        }
        await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false);
    }

    private static bool IsOrdinaryDragonSkinRefusal(string reason) => reason is "skin is already owned" or
        "skin is not owned" or "skin has no legacy item mapping" or "inventory is full" ||
        reason.StartsWith("itemId=", StringComparison.Ordinal);
}
