using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal sealed class DivineGuardChange { }
    private sealed record DivineGuardState(LegacyNativeDivineGuardBindings Bindings, State Before,
        PreparedChange? Change, bool Present, uint[]? Words);
    private readonly ConditionalWeakTable<DivineGuardChange, DivineGuardState> divineGuardChanges = new();

    internal bool TryPrepareDivineGuard(LegacyNativeDivineGuardBindings bindings, out DivineGuardChange? token)
    {
        token = null;
        if (!Monitor.IsEntered(SyncRoot) || !ReferenceEquals(bindings.Vector, this)) return false;
        try
        {
            if (!TryCapture(out var before)) return false;
            bool present = bindings.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.DivineGuard);
            if (!IsCurrent(before!) || !bindings.IsCurrent()) return false;
            PreparedChange? change = null; uint[]? words = null;
            if (!present && (!TryPrepareAdd((int)LegacyNativeMsgUpdate.Flags.DivineGuard, 15, true, 0, false, out change) ||
                !TryGetPreparedWords(change, out words))) return false;
            if (!IsCurrent(before!) || !bindings.IsCurrent()) return false;
            var prepared = new DivineGuardChange();
            divineGuardChanges.Add(prepared, new(bindings, before!, change, present, words));
            token = prepared;
            return true;
        }
        catch (Exception) { return false; }
    }

    internal bool TryGetDivineGuardPublication(DivineGuardChange token, LegacyNativeDivineGuardBindings bindings,
        out bool present, out uint[]? words)
    {
        present = false; words = null;
        if (!Monitor.IsEntered(SyncRoot) || token is null ||
            !divineGuardChanges.TryGetValue(token, out var state) || !ReferenceEquals(state.Bindings, bindings) ||
            !IsCurrent(state.Before)) return false;
        present = state.Present; words = state.Words?.ToArray();
        return true;
    }

    internal bool TryAcceptDivineGuard(DivineGuardChange token, LegacyNativeDivineGuardBindings bindings,
        LegacyNativePreparedDivineGuardPublication publication, out bool grant)
    {
        grant = false;
        if (!Monitor.IsEntered(bindings.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(bindings.Player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(SyncRoot) ||
            !ReferenceEquals(bindings.Vector, this) || token is null ||
            !divineGuardChanges.TryGetValue(token, out var state) || !ReferenceEquals(state.Bindings, bindings)) return false;
        divineGuardChanges.Remove(token);
        try
        {
            PreparedState? staged = null;
            if (state.Change is not null && !TryTakeCurrent(state.Change, out staged)) return false;
            if (!IsCurrent(state.Before) ||
                !publication.MatchesAcceptance(bindings, token, state.Present, state.Words) ||
                !bindings.IsCurrent() || !publication.HasLiveStorage()) return false;
            // Present registration skips the genuine chance entirely. An eligible
            // loss still owns all three possible-success loans before the draw.
            bool selected = !state.Present && LegacyNativeRoleCore.Rate(5);
            // Reuse the actual reviewed pure physical check, not another bitmap
            // authority or comparer lookup after the real one-draw boundary.
            if (!RefinementPhysicalCurrent(state.Before) || !bindings.IsCurrent() || !publication.HasLiveStorage()) return false;
            if (selected && staged is not null) CommitPreparedState(staged);
            bindings.Player.LastDivineGuard = bindings.Stamp;
            grant = selected;
            return true;
        }
        catch (Exception) { return false; }
    }
}
