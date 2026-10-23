namespace WolfConquer.Core.World;

internal partial class LegacyNativePerfectionEffects
{
    // Transaction for a genuinely selected native trigger, not a public chance/
    // prestige/cooldown authorization API or a replacement CheckEffects dispatcher.
    internal static bool TryDivineGuard(LegacyNativeGameClientRuntime? client)
    {
        if (client?.Player is not { } player) return false;
        bool accepted = false;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                var vector = player.BitVector; var view = player.View;
                if (vector is null || view is null) return false;
                lock (vector.SyncRoot)
                {
                    var bindings = new LegacyNativeDivineGuardBindings(client, player, vector, view);
                    if (!bindings.IsCurrent() || !vector.TryPrepareDivineGuard(bindings, out var token)) return false;
                    using var publication = LegacyNativePreparedDivineGuardPublication.Create(bindings, token!);
                    if (!vector.TryAcceptDivineGuard(token!, bindings, publication, out bool grant)) return false;
                    accepted = true;
                    publication.Publish(grant);
                    return true;
                }
            }
        }
        catch (Exception error)
        {
            LegacyNativePlayerRuntime.ReportFlagAdmission(accepted ?
                "DivineGuard notification after acceptance" : "DivineGuard preparation", error);
            return accepted;
        }
    }
}
