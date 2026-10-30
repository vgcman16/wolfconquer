namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitLegacyBankCps(WorldPacketHandlerContext context, WorldSession current,
        LegacyBankCpsTransferPlan plan, out string reason)
    {
        reason = "bankCpsPersistenceOwnerUnavailable";
        if (context.Sessions is not { } sessions) return false;
        // Existing entirely in-memory/no-native compatibility owns no files.
        // Its current CAS still consumes the pending amount only on success.
        if (BankCpsMemoryOnly(context, current))
        {
            bool changed = sessions.TryCompareAndSet(context.Connection, current, plan.Updated, out _);
            reason = changed ? string.Empty : "bankCpsCurrentOwnerChanged";
            return changed;
        }
        if (!sessions.IsBankCpsFrameContext(context, current)) return false;
        var failure = string.Empty;
        bool ActualWriter()
        {
            var items = sessions.RequireBankCpsWriterContext(context, current);
            if (context.TradeRecovery!.IsBlocked || context.Trades!.HasConnectionEnded(context.Connection) ||
                !LegacySecondaryPasswordRuntime.IsAdmitted(context.Connection, plan.PersistenceBefore) ||
                !WarehouseMoneyTradeClear(context, plan.PersistenceBefore) ||
                current.PendingBankConquerPoints != plan.Amount || current.PendingBankConquerPointsIsDeposit != plan.Deposit)
            { failure = "bankCpsCurrentPermissionChanged"; return false; }
            if (!plan.Inputs.IsCurrentPlain()) { failure = "bankCpsPreparedInputChanged"; return false; }
            return LegacyNpcSalePersistence.TryPersistBankCps(plan.PersistenceBefore, plan.Updated, items,
                plan.Deposit, plan.Amount, out failure);
        }
        if (sessions.TryCommitSession(context.Connection, current, plan.Updated, ActualWriter, out var final))
        {
            if (final is null) throw new LegacyCommittedSpellPublicationException();
            reason = string.Empty; return true;
        }
        if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) context.TradeRecovery!.Block(failure);
        reason = failure.Length == 0 ? "bankCpsCurrentOwnerChanged" : failure;
        return false;
    }
}
