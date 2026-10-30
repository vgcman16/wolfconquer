using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyBankCpsMutationAsync(WorldPacketHandlerContext context,
        WorldSession supplied, byte option, string input, bool requireActiveNpc, CancellationToken cancellationToken)
    {
        var reason = "bankCpsOwnerUnavailable"; byte[][] frames = []; LegacyBankCpsTransferPlan? committed = null;
        // An implicit-NPC routing hint can become stale while Frame waits. Refuse
        // a retained Frame lease instead of nesting Shared -> Exclusive.
        if (context.TradeOperations?.OwnsActiveLease(context.FrameOperationLease) == true)
        {
            await context.Log.WriteLineAsync("WorldServer skipped LegacyNpc BankCps mutation; bankCpsFrameLeaseRetained.")
                .ConfigureAwait(false);
            return true;
        }
        using (var operation = context.TradeOperations is { } operations ?
            await operations.EnterPacketExclusiveAsync(cancellationToken).ConfigureAwait(false) : null)
        {
            if (TryResolveBankCpsOwner(context, supplied, requireActiveNpc, confirmation: option is 2 or 3, out var current, out reason))
            {
                bool deposit = option is 2 or 10;
                if (option is 10 or 20)
                    frames = PrepareBankCpsPending(context, current, input, deposit, out reason);
                else if (TryPrepareBankCpsTransfer(current, deposit, out var plan, out reason, out var rejection))
                {
                    if (TryCommitLegacyBankCps(context, current, plan, out reason))
                    { committed = plan; frames = plan.Frames; }
                }
                else frames = rejection;
            }
        }
        foreach (var frame in frames) await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (committed is { } success)
            await context.Log.WriteLineAsync("WorldServer applied LegacyNpc BankCps transfer " +
                $"{FormatSession(context with { Session = success.Updated })} source={LegacyBankCpsSource} " +
                $"mode={(success.Deposit ? "deposit" : "withdraw")} amount={success.Amount} " +
                $"conquerPoints={success.Updated.Character.ConquerPoints} bankCps={success.Updated.Character.BankConquerPoints}.")
                .ConfigureAwait(false);
        else await context.Log.WriteLineAsync($"WorldServer handled LegacyNpc BankCps input/refusal {FormatSession(context)} reason={reason}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool BankCpsMemoryOnly(WorldPacketHandlerContext context, WorldSession current) =>
        context.Connection.NativeGameClient is null && current.NativeGameClient is null &&
        string.IsNullOrWhiteSpace(current.UsersRoot) && string.IsNullOrWhiteSpace(context.PlayerItemsRoot);

    private static bool TryResolveBankCpsOwner(WorldPacketHandlerContext context, WorldSession supplied, bool requireActiveNpc, bool confirmation,
        out WorldSession current, out string reason)
    {
        current = null!; reason = "bankCpsOwnerUnavailable";
        if (context.Sessions is not { } sessions || !sessions.TryGet(context.Connection, out var found) || found is null ||
            found.AccountId != supplied.AccountId || found.Character.Uid != supplied.Character.Uid ||
            !ReferenceEquals(found.NativeGameClient, supplied.NativeGameClient) || confirmation && !ReferenceEquals(found, supplied) ||
            requireActiveNpc && found.ActiveNpcId != LegacyBankCpsNpcId) return false;
        current = found;
        if (context.TradeRecovery?.IsBlocked == true || context.Trades?.HasConnectionEnded(context.Connection) == true ||
            !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, current) || !WarehouseMoneyTradeClear(context, current))
        { reason = "bankCpsCurrentPermissionInvalid"; return false; }
        if (BankCpsMemoryOnly(context, current)) return true;
        if (context.TradeOperations is null || context.TradeRecovery is null || context.Trades is null || context.WorldData is null ||
            !sessions.IsBankCpsFrameContext(context, current) ||
            !CanUseLegacyNpcServerClick(context, current.Character, LegacyBankCpsNpcId, out _)) return false;
        if (current.NativeGameClient is { } client && (client.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(binding.Store, sessions) || !ReferenceEquals(binding.Client, client) ||
            !sessions.IsBoundSpellOwnerCurrent(binding))) return false;
        reason = string.Empty; return true;
    }

    private static byte[][] PrepareBankCpsPending(WorldPacketHandlerContext context, WorldSession current,
        string input, bool deposit, out string reason)
    {
        reason = "bankCpsInputInvalid";
        if (!uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount == 0)
            return [LegacyNpcReplyPacket.ForDialog("Please enter a valid CP amount.").ToFrameBytes(),
                LegacyNpcReplyPacket.ForInput(deposit ? "Deposit CPs" : "Withdraw CPs", deposit ? (byte)10 : (byte)20, 10).ToFrameBytes(),
                LegacyNpcReplyPacket.ForClosedOption("Later.").ToFrameBytes(), LegacyNpcReplyPacket.ForAvatar(183).ToFrameBytes(),
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes()];
        if (current.Character.ConquerPoints < 0 || current.Character.BankConquerPoints < 0)
        { reason = "bankCpsWalletInvalid"; return []; }
        if ((deposit ? current.Character.ConquerPoints : current.Character.BankConquerPoints) < amount)
        { reason = "bankCpsFundsChanged"; return BankCpsFundsFrames(deposit); }
        var next = current with { ActiveNpcId = LegacyBankCpsNpcId, PendingBankConquerPoints = amount,
            PendingBankConquerPointsIsDeposit = deposit };
        var frames = BankCpsDialogFrames("Are you sure you want to " + (deposit ? "deposit " : "withdraw ") +
            amount.ToString(CultureInfo.InvariantCulture) + " CPs" + (deposit ? " into your bank?" : " from your bank?"), 48,
            new LegacyNpcDialogOption("Yes.", deposit ? (byte)2 : (byte)3), new LegacyNpcDialogOption("No thanks.", byte.MaxValue));
        if (!context.Sessions!.TryCompareAndSet(context.Connection, current, next, out _))
        { reason = "bankCpsPendingOwnerChanged"; return []; }
        reason = "pendingConfirmationPrepared"; return frames;
    }
}
