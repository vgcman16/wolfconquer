using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class SqlAccountWorldHandoffCleanupTests
{
    private sealed class CleanupWitness(string name, List<string> events, params Exception[] failures) : IAsyncDisposable
    {
        internal int Calls { get; private set; }
        public ValueTask DisposeAsync()
        {
            int call = ++Calls;
            events.Add(name + ":" + call);
            if (call <= failures.Length) ThrowAtDisposalSite(failures[call - 1]);
            return ValueTask.CompletedTask;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void ThrowAtDisposalSite(Exception failure) => ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class BlockingCleanupWitness(Exception failure) : IAsyncDisposable
    {
        private int calls;
        internal int Calls => Volatile.Read(ref calls);
        internal TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            Entered.TrySetResult(true);
            await Release.Task;
            CleanupWitness.ThrowAtDisposalSite(failure);
        }
    }

    private static IOException CapturePrimaryFailure()
    {
        try { throw new IOException("originalHandoffFailure"); }
        catch (IOException failure) { return failure; }
    }

    private static async Task<Exception?> ObserveAsync(Task task)
    {
        try { await task; return null; }
        catch (Exception failure) { return failure; }
    }
}
