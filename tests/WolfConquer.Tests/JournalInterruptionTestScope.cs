using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal static class JournalInterruptionTestScope
{
    // The assertion body runs outside the engine. It is not passed to a writer.
    internal static void Run(LegacyJournalInterruption interruption, Action<LegacyJournalInterruption> assertions)
    {
        Exception? primary = null;
        try { assertions(interruption); } catch (Exception error) { primary = error; }
        try { interruption.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException(primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
