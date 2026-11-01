namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Called only with the exact actual holder/binding and their gates held.
    private static void FenceContinuationOutcome(SessionHolder holder, LegacyOrdinarySpellBinding binding, Exception primary)
    {
        var errors = new List<Exception> { primary };
        try { binding.Recovery.Block("experienceContinuationRecoveryRequired:outcomeUnproven"); }
        catch (Exception error) { errors.Add(error); }
        try { holder.Clear(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException("Experience continuation outcome and fencing failed.", errors);
    }

    private static void FenceContinuationDetached(LegacyOrdinarySpellBinding binding, Exception primary)
    {
        try { binding.Recovery.Block("experienceContinuationRecoveryRequired:ownerChanged"); }
        catch (Exception secondary) { throw new AggregateException("Experience continuation and recovery fencing failed.", primary, secondary); }
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
