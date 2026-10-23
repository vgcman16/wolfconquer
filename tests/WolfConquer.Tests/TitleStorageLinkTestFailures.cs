using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

// Test-only failure reporting: every exact-owned cleanup is attempted. A cleanup
// failure cannot hide an assertion, skip, or an earlier cleanup failure.
internal static class TitleStorageLinkTestFailures
{
    internal static async Task RunAsync(Func<Task> body, params Action[] cleanup)
    {
        List<Exception>? failures = null;
        try { await body().ConfigureAwait(false); }
        catch (Exception error) { failures = [error]; }
        foreach (var action in cleanup)
        {
            try { action(); }
            catch (Exception error) { (failures ??= []).Add(error); }
        }
        if (failures is null) return;
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        throw new AggregateException("Title link control and/or exact owned cleanup failed; all failures are retained.", failures);
    }
}
