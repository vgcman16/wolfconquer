namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Fixed reasons only. These describe failed real proofs and confer no
    // authority. The bitset bounds logging for the lifetime of this Frame.
    internal enum MonsterPhysicalAiRefusal
    {
        None, PhysicalRange, TargetAbsent, PhaseOwner, TargetStore, TargetHolder,
        TargetBinding, TargetNativeOwner, TargetScalars, TargetFlags, TargetSnapshot,
        StoreOwner, ActionContext, ConnectionGate, StoreHolder, StoreBinding,
        Lifecycle, TargetCustody, PhaseTarget, MonsterOwner, MonsterState,
        MonsterBirth, PhaseConsumed
    }

    private long diagnosedMonsterPhysicalAiReasons;

    internal static bool RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal failure,
        out MonsterPhysicalAiRefusal reason)
    {
        reason = failure;
        return false;
    }

    // Called by the private loop only after the phase/store/actor gates exit.
    private async ValueTask LogMonsterPhysicalAiRefusalAsync(MonsterPhysicalAiRefusal reason)
    {
        if (reason == MonsterPhysicalAiRefusal.None) return;
        long bit = 1L << (int)reason;
        if ((Interlocked.Or(ref diagnosedMonsterPhysicalAiReasons, bit) & bit) != 0) return;
        try
        {
            await _log.WriteLineAsync($"WorldServer monster physical AI unavailable reason={reason}.")
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            // Diagnostics cannot veto the real no-target transition or retry a
            // refused phase. Do not recursively log, retry, or swallow fatal faults.
        }
    }
}
