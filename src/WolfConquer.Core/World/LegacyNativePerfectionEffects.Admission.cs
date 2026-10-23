namespace WolfConquer.Core.World;

internal partial class LegacyNativePerfectionEffects
{
    internal enum RefinementKind { Mirror, Killing, Stamina }

    internal static bool TryMirrorOfSin(LegacyNativePlayerRuntime? target) =>
        TryRefinement(target?.Owner, target, RefinementKind.Mirror);

    internal static bool TryKillingFlash(LegacyNativeGameClientRuntime? client) =>
        TryRefinement(client, client?.Player, RefinementKind.Killing);

    internal static bool TryLightOfStamina(LegacyNativeGameClientRuntime? client) =>
        TryRefinement(client, client?.Player, RefinementKind.Stamina);

    private static bool TryRefinement(LegacyNativeGameClientRuntime? client,
        LegacyNativePlayerRuntime? player, RefinementKind kind)
    {
        if (client is null || player is null) return false;
        bool accepted = false;
        try
        {
            // This is the same cooperating owner order as spell/flag/Sage admission.
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                var vector = player.BitVector;
                var view = player.View;
                if (vector is null || view is null) return false;
                lock (vector.SyncRoot)
                {
                    var bindings = new LegacyNativeRefinementBindings(client, player, vector, view, kind);
                    if (!bindings.IsCurrent() || !vector.TryPrepareRefinement(bindings, out var token)) return false;
                    using var publication = LegacyNativePreparedRefinementPublication.Create(bindings, token!);
                    if (!vector.TryAcceptRefinement(token!, bindings, publication, out bool grant)) return false;
                    accepted = true;
                    publication.Publish(grant);
                    return true;
                }
            }
        }
        catch (Exception error)
        {
            LegacyNativePlayerRuntime.ReportFlagAdmission(accepted ?
                "refinement notification after acceptance" : "refinement preparation", error);
            return accepted;
        }
    }
}
