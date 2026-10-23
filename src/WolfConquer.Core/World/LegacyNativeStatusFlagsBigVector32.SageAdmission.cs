namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal bool TryCommitSage(PreparedChange token, LegacyNativeSageModeBindings bindings,
        LegacyNativePreparedSagePublication publication)
    {
        if (!ReferenceEquals(bindings.Vector, this) || !Monitor.IsEntered(bindings.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(bindings.Player.SpellAdmissionSyncRoot) || !TryTakeCurrent(token, out var staged)) return false;
        // The genuine comparer has returned. Only private staged policy,
        // exact plan/owner/word evidence and bounded prepared-byte checks remain.
        var row = PreparedSageAddition(staged!);
        if (row is null || !publication.MatchesPreparedPlan(bindings, token, staged!.Words, row.Secounds) ||
            !bindings.IsCurrent() || !publication.HasPreparedStorage()) return false;
        CommitPreparedState(staged!);
        return true;
    }

    private static Flag? PreparedSageAddition(PreparedState staged)
    {
        int flag = (int)LegacyNativeMsgUpdate.Flags.SageSpeedMode;
        foreach (var saved in staged.Before.Rows)
            if (saved.Key == flag) return null; // A replacement is not an absent Sage grant.
        Flag? sage = null;
        foreach (var row in staged.Cache)
        {
            if (row is null) return null;
            if (row.Key != flag) continue;
            if (sage is not null) return null;
            sage = row;
        }
        return sage is not null && sage.RemoveOnDead && sage.InvokerSecouds == 0 &&
            sage.TimerInvoker == default && TryDeadline(sage.Timer, sage.Secounds, out _) ? sage : null;
    }
}
