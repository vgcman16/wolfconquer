using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatusFlagsBigVector32
{
    internal sealed class SelfStatusChange { }
    private sealed record SelfStatusState(State Before, PreparedChange? Change, int Flag, bool Present, uint[]? ExpectedWords);
    private readonly ConditionalWeakTable<SelfStatusChange, SelfStatusState> selfStatusChanges = new();

    internal bool TryPrepareSelfStatus(int flag, int seconds, out SelfStatusChange? token,
        out bool present, out uint[]? statusWords)
    {
        token = null; present = false; statusWords = null;
        if (!Monitor.IsEntered(SyncRoot) ||
            !((flag == (int)LegacyNativeMsgUpdate.Flags.Stigma && seconds == 90) ||
              (flag == (int)LegacyNativeMsgUpdate.Flags.MagicShield && seconds == 40))) return false;
        try
        {
            if (!TryCapture(out var before) || !ValidIndex(flag, before!.Words)) return false;
            present = before.Rows.Any(row => row.Key == flag);
            PreparedChange? change = null;
            if (!present && (!TryPrepareAdd(flag, seconds, true, 0, false, out change) ||
                !TryGetPreparedWords(change, out statusWords))) return false;
            var prepared = new SelfStatusChange();
            selfStatusChanges.Add(prepared, new(before, change, flag, present, statusWords?.ToArray()));
            token = prepared;
            return true;
        }
        catch (Exception) { return false; }
    }

    internal bool TryAcceptSelfStatus(SelfStatusChange token, LegacyNativeSelfStatusBindings bindings,
        LegacyNativePreparedSelfStatusPublication publication)
    {
        if (!Monitor.IsEntered(bindings.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(bindings.Player.SpellAdmissionSyncRoot) || !Monitor.IsEntered(SyncRoot) ||
            !ReferenceEquals(bindings.Vector, this) || token is null ||
            !selfStatusChanges.TryGetValue(token, out var prepared)) return false;
        selfStatusChanges.Remove(token);
        try
        {
            PreparedState? staged = null;
            if (prepared.Change is not null && !TryTakeCurrent(prepared.Change, out staged)) return false;
            if (!IsCurrent(prepared.Before) || !TryCapture(out var current) ||
                !SamePhysicalMonsterState(prepared.Before, current!) ||
                (staged is not null && !SamePhysicalMonsterState(staged.Before, current!)) ||
                !publication.MatchesAcceptance(bindings, token, prepared.Flag, prepared.Present, prepared.ExpectedWords) ||
                !bindings.IsCurrent() || !publication.HasLiveStorage()) return false;
            // Full comparer-aware revalidation, pure recapture, physical ownership,
            // caller storage and both prepared loans are complete. The only mutation
            // is the genuine existing three-reference COW status commit.
            if (staged is not null) CommitPreparedState(staged);
            return true; // A prepared present flag is accepted without renewal/cache writes.
        }
        catch (Exception) { return false; }
    }
}
