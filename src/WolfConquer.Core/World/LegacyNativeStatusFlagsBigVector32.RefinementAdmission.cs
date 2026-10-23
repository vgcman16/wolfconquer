using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal sealed class RefinementChange { }
    private sealed record RefinementState(LegacyNativeRefinementBindings Bindings, State Before, PreparedChange? Change,
        LegacyNativePerfectionEffects.RefinementKind Kind, bool Eligible, uint[]? Words, ushort Stamina);
    private readonly ConditionalWeakTable<RefinementChange, RefinementState> refinementChanges = new();

    internal bool TryPrepareRefinement(LegacyNativeRefinementBindings bindings, out RefinementChange? token)
    {
        token = null;
        if (!Monitor.IsEntered(SyncRoot) || !ReferenceEquals(bindings.Vector, this)) return false;
        try
        {
            if (!TryCapture(out var before)) return false;
            ushort stamina = bindings.Player.Stamina;
            // Keep the genuine predicate priority, including OnXPSkill's complete
            // fourteen-flag query family. Invalid is never treated as absence.
            bool eligible = bindings.Kind switch
            {
                LegacyNativePerfectionEffects.RefinementKind.Mirror =>
                    bindings.Player.OnXPSkill() == LegacyNativeMsgUpdate.Flags.Normal &&
                    !bindings.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.XPList),
                LegacyNativePerfectionEffects.RefinementKind.Killing =>
                    !bindings.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.XPList) &&
                    bindings.Player.OnXPSkill() == LegacyNativeMsgUpdate.Flags.Normal,
                LegacyNativePerfectionEffects.RefinementKind.Stamina => stamina < 100,
                _ => throw new InvalidDataException("Unknown refinement admission."),
            };
            if (!IsCurrent(before!) || !bindings.IsCurrent()) return false;
            PreparedChange? change = null;
            uint[]? words = null;
            if (eligible && bindings.Kind != LegacyNativePerfectionEffects.RefinementKind.Stamina &&
                (!TryPrepareAdd((int)LegacyNativeMsgUpdate.Flags.XPList, 20, true, 0, false, out change) ||
                 !TryGetPreparedWords(change, out words))) return false;
            if (!IsCurrent(before!) || !bindings.IsCurrent() ||
                (bindings.Kind == LegacyNativePerfectionEffects.RefinementKind.Stamina && bindings.Player.Stamina != stamina)) return false;
            var prepared = new RefinementChange();
            refinementChanges.Add(prepared, new(bindings, before!, change, bindings.Kind, eligible, words, stamina));
            token = prepared;
            return true;
        }
        catch (Exception) { return false; }
    }

    internal bool TryGetRefinementPublication(RefinementChange token, LegacyNativeRefinementBindings bindings,
        out LegacyNativePerfectionEffects.RefinementKind kind, out bool eligible, out uint[]? words)
    {
        kind = default; eligible = false; words = null;
        if (!Monitor.IsEntered(SyncRoot) || token is null ||
            !refinementChanges.TryGetValue(token, out var state) || !ReferenceEquals(state.Bindings, bindings) ||
            !IsCurrent(state.Before)) return false;
        kind = state.Kind; eligible = state.Eligible; words = state.Words?.ToArray();
        return true;
    }

    internal bool TryAcceptRefinement(RefinementChange token, LegacyNativeRefinementBindings bindings,
        LegacyNativePreparedRefinementPublication publication, out bool grant)
    {
        grant = false;
        if (!Monitor.IsEntered(bindings.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(bindings.Player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(SyncRoot) ||
            !ReferenceEquals(bindings.Vector, this) || token is null ||
            !refinementChanges.TryGetValue(token, out var state) || !ReferenceEquals(state.Bindings, bindings)) return false;
        refinementChanges.Remove(token);
        try
        {
            PreparedState? staged = null;
            if (state.Change is not null && !TryTakeCurrent(state.Change, out staged)) return false;
            if (!IsCurrent(state.Before) ||
                !publication.MatchesAcceptance(bindings, token, state.Kind, state.Eligible, state.Words) ||
                !RefinementBindingsCurrent(state, bindings) || !publication.HasLiveStorage()) return false;

            // Everything comparer-dependent, allocated and serialized is complete
            // before this one genuine shared-Pool draw. A losing eligible Killing
            // still owns three bounded loans; this is not an allocation optimization.
            bool selected = state.Eligible &&
                (state.Kind != LegacyNativePerfectionEffects.RefinementKind.Killing || LegacyNativeRoleCore.Rate(10));

            // Pure physical/lifetime checks only after Rate: no comparer, serializer,
            // allocation, clock callback or fallible gameplay operation before commit.
            // Raw external mutation may refuse here; RNG consumption is not rolled back.
            if (!RefinementPhysicalCurrent(state.Before) ||
                !RefinementBindingsCurrent(state, bindings) || !publication.HasLiveStorage()) return false;
            if (selected && staged is not null) CommitPreparedState(staged);
            if (selected && state.Kind == LegacyNativePerfectionEffects.RefinementKind.Stamina)
                bindings.Player.Stamina = 100;
            grant = selected;
            return true;
        }
        catch (Exception) { return false; }
    }

    private static bool RefinementBindingsCurrent(RefinementState state, LegacyNativeRefinementBindings bindings) =>
        state.Kind == bindings.Kind && bindings.IsCurrent() &&
        (state.Kind != LegacyNativePerfectionEffects.RefinementKind.Stamina || bindings.Player.Stamina == state.Stamina);

    private bool RefinementPhysicalCurrent(State before)
    {
        if (!ReferenceEquals(ArrayFlags, before.Registrations) || !ReferenceEquals(bits, before.Words) ||
            ArrayFlags.Count != before.Rows.Length || !bits.AsSpan().SequenceEqual(before.SnapshotWords)) return false;
        foreach (var saved in before.Rows)
        {
            var row = saved.Row;
            if (row.Key != saved.Key || row.Secounds != saved.Seconds || row.InvokerSecouds != saved.Invoker ||
                row.Timer != saved.Timer || row.TimerInvoker != saved.InvokeTimer || row.RemoveOnDead != saved.RemoveOnDeath) return false;
        }
        return true;
    }
}
