using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimeReviverRaceTests
{
    private static async Task RunAsync(Func<Fixture, Task> body)
    {
        var fixture = new Fixture();
        var errors = new List<Exception>();
        try { await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupAsync(errors); }
        catch (Exception error) { fixture.Retain(); AddFailure(errors, error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException($"Reviver race fixture failed; ownedRoot={fixture.Root}", errors);
    }

    private static void AddFailure(List<Exception> errors, Exception error)
    { if (!errors.Any(existing => ReferenceEquals(existing, error))) errors.Add(error); }

    private sealed partial class Fixture
    {
        private static readonly ConcurrentBag<Fixture> Retained = new();
        private readonly HashSet<Task> cleanupObserved = [];
        private int retained;
        private Task? cancellation;
        private Task? timerDisposal;
        private Task? transportClose;
        private Task? exclusiveProbe;
        private CancellationTokenSource? exclusiveProbeBudget;
        private Task? handlerDrain;

        internal async Task CleanupAsync(List<Exception> errors)
        {
            // Release only this test's observation hold. No fabricated rollback,
            // private runtime callback or manually set start/finish flag.
            ReleasePlayerFive.TrySetResult();
            var complete = true;
            foreach (var actual in frameCalls)
                if (!await ObserveAsync(actual, errors)) complete = false;
            cancellation ??= lifetime.CancelAsync();
            complete &= await ObserveAsync(cancellation, errors);
            if (!complete || !cancellation.IsCompletedSuccessfully)
            { RetainUnknown(errors, "Frame/cancellation task"); return; }
            if (!await DrainRuntimeWorkersAsync(errors))
            { RetainUnknown(errors, "actual retained runtime workers"); return; }
            timerDisposal ??= Clock.WaitForDisposedAsync();
            if (!await ObserveAsync(timerDisposal, errors)) { RetainUnknown(errors, "actual delay disposal"); return; }
            if (handler is not null)
            {
                var trades = ReadField<LegacyTradeStateStore>("_tradeStates");
                lock (trades.SyncRoot)
                    if (connection is not null && trades.Contains(connection))
                    { RetainUnknown(errors, "unexpected trade ownership"); return; }
            }
            if (connection is not null)
            {
                transportClose ??= connection.CompleteTransportCloseAsync();
                if (!await ObserveAsync(transportClose, errors) || !transportClose.IsCompletedSuccessfully)
                { RetainUnknown(errors, "actual transport cleanup"); return; }
            }
            if (handler is not null)
            {
                exclusiveProbeBudget ??= new CancellationTokenSource(Bound);
                exclusiveProbe ??= ProbeAsync(ReadField<WorldOperationBarrier>("_worldOperations"), exclusiveProbeBudget.Token);
                if (!await ObserveAsync(exclusiveProbe, errors) || !exclusiveProbe.IsCompletedSuccessfully)
                { RetainUnknown(errors, "actual shared operation release"); return; }
                try { await WaitUntilAsync(() => ReadField<int>("_legacyRuntimeGlobalLoopStarted") == 0); }
                catch (Exception error) { AddFailure(errors, error); RetainUnknown(errors, "actual global finally"); return; }
                // An optional handler-wide owner is distinct from the two
                // actual runtime tasks retained and awaited above.
                if ((object)handler is IAsyncDisposable owned)
                {
                    handlerDrain ??= owned.DisposeAsync().AsTask();
                    if (!await ObserveAsync(handlerDrain, errors) || !handlerDrain.IsCompletedSuccessfully)
                    { RetainUnknown(errors, "actual handler drain"); return; }
                }
            }
            if (!Clock.AllDisposed || !lifetime.IsCancellationRequested || log.ToString().Contains("PARITY-BUG", StringComparison.Ordinal))
            { RetainUnknown(errors, "normal canceled runtime control flow"); return; }
            // The actual initially captured tasks AND later snapshot union
            // have completed successfully with zero recorded worker faults.
            // Timer disposal/probe/global-finally are additional real checks,
            // not substitutes for that join. ActualRearmsObserved records only
            // the successful-case milestone; setup cleanup does not claim it.
            try { exclusiveProbeBudget?.Dispose(); } catch (Exception error) { AddFailure(errors, error); }
            try { lifetime.Dispose(); } catch (Exception error) { AddFailure(errors, error); }
            try { if (ownsRoot) { ValidateRoot(); DeleteOrdinaryTree(Root); } }
            catch (Exception error) { AddFailure(errors, error); }
            try { log.Dispose(); } catch (Exception error) { AddFailure(errors, error); }
        }

        private async Task<bool> DrainRuntimeWorkersAsync(List<Exception> errors)
        {
            if (handler is null) return true;
            var elapsed = Stopwatch.StartNew();
            while (true)
            {
                // Capture retains the genuine active task keys plus its bounded
                // last-fault diagnostic. It does not manufacture completion.
                runtimeWorkers.UnionWith(handler.CaptureLegacyRuntimeWorkerTasks());
                var observed = runtimeWorkers.ToArray();
                var successful = true;
                foreach (var actual in observed)
                {
                    if (!await ObserveAsync(actual, errors) || !actual.IsCompletedSuccessfully)
                        successful = false;
                }
                runtimeWorkers.UnionWith(handler.CaptureLegacyRuntimeWorkerTasks());
                if (!successful) return false;
                if (handler.LegacyRuntimeFaultedWorkerCount != 0)
                {
                    AddFailure(errors, new InvalidOperationException(
                        $"Actual runtime worker fault count was {handler.LegacyRuntimeFaultedWorkerCount}; ownedRoot={Root}"));
                    return false;
                }
                if (runtimeWorkers.Count == observed.Length)
                {
                    if (capturedInitialWorkers) return true;
                    AddFailure(errors, new InvalidOperationException(
                        $"The mandatory two pending startup worker references were not captured; ownedRoot={Root}"));
                    return false;
                }
                if (elapsed.Elapsed >= Bound)
                {
                    AddFailure(errors, new TimeoutException(
                        $"Actual runtime worker snapshot union did not settle; ownedRoot={Root}"));
                    return false;
                }
            }
        }

        private async Task<bool> ObserveAsync(Task actual, List<Exception> errors)
        {
            if (cleanupObserved.Contains(actual)) return true;
            try { await actual.WaitAsync(Bound); }
            catch (Exception observation)
            {
                AddFailure(errors, observation);
                if (!actual.IsCompleted) return false;
                try { await actual; }
                catch (Exception completion) { AddFailure(errors, completion); }
            }
            cleanupObserved.Add(actual);
            return true;
        }

        private static async Task ProbeAsync(WorldOperationBarrier operations, CancellationToken cancellationToken)
        { using var actual = await operations.EnterExclusiveAsync(cancellationToken); }

        private static async Task WaitUntilAsync(Func<bool> predicate)
        {
            var elapsed = Stopwatch.StartNew();
            while (!predicate())
            {
                if (elapsed.Elapsed >= Bound) throw new TimeoutException("Actual runtime finally was not observed.");
                await Task.Delay(10);
            }
        }

        private void RetainUnknown(List<Exception> errors, string boundary)
        {
            Retain();
            AddFailure(errors, new InvalidOperationException($"Reviver race resources retained for unproven {boundary}; ownedRoot={Root}"));
        }
        internal void Retain() { if (Interlocked.Exchange(ref retained, 1) == 0) Retained.Add(this); }

        private void ValidateRoot()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(RootPrefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[RootPrefix.Length..], "N", out _))
                throw new InvalidOperationException("Not the exact owned runtime race root.");
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned runtime race domain has a reparse ancestor.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        private void DeleteOrdinaryTree(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!Path.GetFullPath(entry).StartsWith(Path.GetFullPath(Root) + Path.DirectorySeparatorChar,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Cleanup entry is outside the exact ordinary race domain.");
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }
}
