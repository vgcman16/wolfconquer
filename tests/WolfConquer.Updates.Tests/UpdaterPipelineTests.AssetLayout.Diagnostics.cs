using System.Diagnostics;
using WolfConquer.Updater;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    // Observe the actual workflow. No guard substitution, timeout extension,
    // retry, readiness claim or successful result is supplied by this observer.
    private static async Task<ClientWorkflowResult> RunLayoutWithPhaseDiagnostics(string config,
        bool fresh = false, bool repair = false, CancellationToken cancellation = default)
    {
        var phases = new LayoutPhaseDiagnostics();
        try
        {
            return await LauncherWorkflow.RunClientAsync(config, fresh: fresh, repair: repair,
                progress: phases, cancellationToken: cancellation);
        }
        catch (Exception failure)
        {
            throw new InvalidOperationException(
                $"Actual layout workflow failed; fresh={fresh}, repair={repair}, " +
                $"cancellationRequested={cancellation.IsCancellationRequested}. {phases.Describe()}", failure);
        }
    }

    private sealed class LayoutPhaseDiagnostics : IProgress<UpdateOperationProgress>
    {
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private readonly object gate = new();
        private readonly Queue<(long Milliseconds, UpdateOperationPhase Phase)> phases = new();
        private long observed;

        public void Report(UpdateOperationProgress value)
        {
            lock (gate)
            {
                observed++;
                if (phases.Count == 32) phases.Dequeue();
                phases.Enqueue((elapsed.ElapsedMilliseconds, value.Phase));
            }
        }

        internal string Describe()
        {
            lock (gate)
                return $"ElapsedMs={elapsed.ElapsedMilliseconds}; phaseEvents={observed}; " +
                    "lastPhases=" + string.Join(",", phases.Select(row => $"{row.Milliseconds}ms:{row.Phase}"));
        }
    }
}
