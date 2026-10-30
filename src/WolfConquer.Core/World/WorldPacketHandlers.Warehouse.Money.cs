using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleWarehouseMoneyMutationAsync(WorldPacketHandlerContext context,
        WorldSession supplied, MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        var reason = "currentSessionMissing";
        LegacyWarehouseMoneyPlan? committed = null;
        // Dispatch gives parsed deposits/withdrawals their own Exclusive. Do
        // not retain Shared while acquiring it, or await sends inside it.
        using (var operation = context.TradeOperations is { } operations
            ? await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        {
            if (context.TradeRecovery?.IsBlocked == true) reason = "recoveryRequired";
            else if (context.Trades?.HasConnectionEnded(context.Connection) == true) reason = "connectionEnded";
            else if (!TryAdmitLegacySecondaryPasswordItemUsage(context, supplied, out var current, out reason)) { }
            else if (!WarehouseMoneyTradeClear(context, current)) reason = "tradeOrEscrowActiveOrInvalid";
            else if (!TryPlanWarehouseMoneyMutation(current, packet, out var plan, out reason)) { }
            else if (TryCommitWarehouseMoneyMutation(context, current, plan, out var final, out reason))
            {
                if (final is not null && LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, final)) committed = plan;
                else
                {
                    reason = "warehouseMoneyRecoveryRequired:ownerChangedAfterCommit";
                    context.TradeRecovery?.Block(reason);
                }
            }
        }
        if (committed is not null)
        {
            foreach (var frame in committed.Frames)
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync("WorldServer handled MsgItemUsage warehouse " +
                $"{(committed.Deposit ? "deposit" : "withdraw")} {FormatSession(context)} amount={committed.Amount}; " +
                $"persistedBeforeReply={!string.IsNullOrWhiteSpace(committed.Updated.UsersRoot)}.").ConfigureAwait(false);
        }
        else await context.Log.WriteLineAsync("WorldServer skipped MsgItemUsage warehouse " +
            $"{(packet.Action == MsgItemUsageAction.DepositWarehouse ? "deposit" : "withdraw")} " +
            $"{FormatSession(context)}; {reason}.").ConfigureAwait(false);
    }

    private static bool WarehouseMoneyTradeClear(WorldPacketHandlerContext context, WorldSession current) =>
        !current.LegacyTradeActive && context.Trades?.Contains(context.Connection) != true &&
        LegacyTradeEscrowClaims.TryRead(current.Character.Attributes, out var claim, out _) && claim is null;

    private static bool TryCommitWarehouseMoneyMutation(WorldPacketHandlerContext context, WorldSession current,
        LegacyWarehouseMoneyPlan plan, out WorldSession? final, out string reason)
    {
        final = null; reason = "warehouseMoneyPersistenceOwnerUnavailable";
        // Preserve only the existing entirely in-memory managed compatibility
        // path. Neither a native owner nor any configured disk route falls back.
        if (context.Connection.NativeGameClient is null && current.NativeGameClient is null &&
            string.IsNullOrWhiteSpace(current.UsersRoot) && string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            if (context.Sessions is not { } memorySessions)
            { final = plan.Updated; reason = string.Empty; return true; }
            var changed = memorySessions.TryCompareAndSet(context.Connection, current, plan.Updated, out final);
            reason = changed ? string.Empty : "warehouseMoneySessionChanged";
            return changed;
        }
        if (context.Sessions is not { } sessions || string.IsNullOrWhiteSpace(current.UsersRoot) ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || context.TradeOperations is null || context.Trades is null ||
            context.TradeRecovery is null || plan.Inputs is null) return false;
        var failure = string.Empty;
        bool ActualWriter()
        {
            if (context.TradeRecovery.IsBlocked || context.Trades.HasConnectionEnded(context.Connection) ||
                !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, plan.PersistenceBefore) ||
                !WarehouseMoneyTradeClear(context, plan.PersistenceBefore))
            { failure = "warehouseMoneyOwnerChangedBeforeWriter"; return false; }
            if (!plan.Inputs.IsCurrentPlain())
            { failure = "warehouseMoneyInputsChangedBeforeWriter"; return false; }
            return LegacyNpcSalePersistence.TryPersistWarehouseMoney(plan.PersistenceBefore, plan.Updated, context.PlayerItemsRoot,
                plan.Deposit, plan.Amount, out failure);
        }
        if (sessions.TryCommitSession(context.Connection, current, plan.Updated, ActualWriter, out final))
        { reason = string.Empty; return true; }
        if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) context.TradeRecovery.Block(failure);
        reason = failure.Length == 0 ? "warehouseMoneySessionChanged" : failure;
        return false;
    }

    private const long LegacyMaxInventoryMoney = 500_000_000_000L;

    private static ulong ReadWarehouseMoney(CharacterSnapshot character)
    {
        if (!LegacyWarehouseMoneyState.TryRead(character.Attributes, out var warehouse))
            throw new InvalidOperationException("warehouseMoneyStateInvalidOrAmbiguous");
        return checked((ulong)warehouse);
    }

    private static ValueTask SendWalletMoneyUpdateAsync(WorldPacketHandlerContext context,
        CharacterSnapshot character, CancellationToken cancellationToken) =>
        SendScalarUpdateAsync(context, character, LegacyMsgUpdateDataType.Money, checked((ulong)character.Money), cancellationToken);
}
