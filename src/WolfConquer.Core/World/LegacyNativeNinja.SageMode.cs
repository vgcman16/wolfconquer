namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void SageMode(uint Timer) => TryAdmitSageMode(Timer);

    internal bool TryAdmitSageMode(uint timer)
    {
        var client = user;
        var player = client?.Player;
        if (client is null || player is null) return false;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            var vector = player.BitVector;
            var view = player.View;
            if (vector is null || view is null) return false;
            lock (vector.SyncRoot)
            {
                var bindings = new LegacyNativeSageModeBindings(this, client, player, vector, view);
                return bindings.IsCurrent() && PrepareCommitAndPublishSageMode(bindings, timer);
            }
        }
    }

    private static bool PrepareCommitAndPublishSageMode(LegacyNativeSageModeBindings bindings, uint timer)
    {
        LegacyNativePreparedSagePublication? publication = null;
        bool committed = false;
        try
        {
            var vector = bindings.Vector;
            int flag = (int)LegacyNativeMsgUpdate.Flags.SageSpeedMode;
            if (!vector.TryContainFlag(flag, out bool present) || !bindings.IsCurrent()) return false;

            LegacyNativeStatusFlagsBigVector32.PreparedChange? change = null;
            if (!present && !vector.TryPrepareAdd(flag, unchecked((int)timer), true, 0, false, out change)) return false;

            // Both complete native2176 frames and distinct, bounded loans exist
            // before any registration/bitmap/cache reference can be published.
            publication = LegacyNativePreparedSagePublication.Create(bindings, timer, change);
            if (present)
            {
                if (!vector.TryContainFlag(flag, out bool stillPresent) || !stillPresent ||
                    !bindings.IsCurrent() || !publication.MatchesDuplicate(bindings)) return false;
            }
            else if (!vector.TryCommitSage(change!, bindings, publication)) return false;

            // A valid duplicate preserves its original row; its acceptance boundary
            // is the prepared publication attempt, not a fictional renewed grant.
            committed = true;
            publication.Publish();
            return true;
        }
        catch (Exception error)
        {
            LegacyNativePlayerRuntime.ReportFlagAdmission(committed ? "Sage publication after commit" : "Sage preparation", error);
            return committed;
        }
        finally { publication?.Dispose(); }
    }
}
