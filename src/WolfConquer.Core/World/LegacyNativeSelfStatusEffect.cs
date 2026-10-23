using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeSelfStatusEffect
{
    internal static bool TryExecute(LegacyNativeGameClientRuntime client, LegacyNativePacket caller, bool stigma)
    {
        if (client is null || caller is null) return false;
        lock (client.SpellAdmissionSyncRoot)
        {
            var player = client.Player;
            if (player is null || !ReferenceEquals(player.Owner, client)) return false;
            lock (player.SpellAdmissionSyncRoot)
            {
                var vector = player.BitVector;
                var view = player.View;
                if (vector is null || view is null || !ReferenceEquals(view.Owner, client)) return false;
                lock (vector.SyncRoot)
                    return PrepareCommitPublish(client, player, vector, view, caller, stigma);
            }
        }
    }

    private static bool PrepareCommitPublish(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        LegacyNativeStatusFlagsBigVector32 vector, LegacyNativeRoleView view, LegacyNativePacket caller, bool stigma)
    {
        LegacyNativePreparedSelfStatusPublication? publication = null;
        bool accepted = false;
        bool unavailable = false;
        try
        {
            var bindings = new LegacyNativeSelfStatusBindings(client, player, vector, view, caller);
            if (!bindings.IsCurrent()) return false;
            int flag = (int)(stigma ? LegacyNativeMsgUpdate.Flags.Stigma : LegacyNativeMsgUpdate.Flags.MagicShield);
            int seconds = stigma ? 90 : 40;
            // Capture UID/owner/caller BEFORE this first comparer-dependent query.
            // Unavailability remains an explicit legacy error, not valid absence.
            if (!vector.TryContainFlag(flag, out _))
            {
                unavailable = true;
                throw new InvalidDataException("Self-status effect membership is unavailable.");
            }
            if (!bindings.IsCurrent()) return false;
            if (!vector.TryPrepareSelfStatus(flag, seconds, out var token, out bool present, out var words)) return false;
            // Actual GamePoll lookup/target projection and serialization can fail here,
            // before any registration, caller-buffer or queued-output change.
            publication = LegacyNativePreparedSelfStatusPublication.Create(bindings, token!, stigma, present, words);
            if (!vector.TryAcceptSelfStatus(token!, bindings, publication)) return false;
            accepted = true;
            // Preserve the native successful caller bytes/Size/cursor4. Only a bounded
            // already-complete frame copy occurs after acceptance, never serialization.
            publication.CopyCallerAndPublish(bindings);
            return true;
        }
        catch (Exception error) when (!unavailable)
        {
            LegacyNativePlayerRuntime.ReportFlagAdmission(accepted ? "self-status publication after commit" : "self-status preparation", error);
            return accepted;
        }
        finally { publication?.Dispose(); }
    }
}
