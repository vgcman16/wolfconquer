using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static readonly ConcurrentBag<VictoryStartupFixture> RetainedVictoryStartupFixtures = [];

    private static async Task RunVictoryStartupAsync(bool quests, Func<VictoryStartupFixture, Task> body)
    {
        var fixture = new VictoryStartupFixture(quests);
        var errors = new List<Exception>();
        try { await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CleanupAsync(errors); }
        catch (Exception error) { errors.Add(error); RetainedVictoryStartupFixtures.Add(fixture); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Victory startup operation/cleanup failures; unfinished owners retain their domain.", errors);
    }

    private sealed partial class VictoryStartupFixture
    {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
        private readonly List<Task> tasks = [];
        private readonly HashSet<Task> observed = [];
        private readonly TaskCompletionSource releaseReader = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? reader;

        private async Task FrameAsync(byte[] bytes)
        {
            var frame = ReadFrame(bytes);
            // Own the actual whole Frame task. None is the fixture's connection
            // lifetime: observation timeout must not queue detached disconnect
            // work through current RegisterTradeDisconnect.
            var actual = Task.Run(async () => await handler!.HandleFrameAsync(connection, frame, CancellationToken.None));
            tasks.Add(actual);
            await ObserveAsync(actual);
        }

        private async Task ObserveAsync(Task actual)
        {
            try { await actual.WaitAsync(Bound + Bound); }
            catch (Exception observation)
            {
                if (actual.IsCompleted)
                {
                    Exception? terminal = null;
                    try { await actual; } catch (Exception error) { terminal = error; }
                    observed.Add(actual);
                    if (terminal is not null && !ReferenceEquals(terminal, observation))
                        throw new AggregateException(observation, terminal);
                }
                throw;
            }
            observed.Add(actual);
        }

        internal async Task HoldIndependentReaderAsync()
        {
            Assert.Null(reader);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            reader = Task.Run(async () =>
            {
                try
                {
                    using var budget = new CancellationTokenSource(Bound);
                    using var lease = await operations.EnterSharedAsync(budget.Token);
                    entered.TrySetResult();
                    await releaseReader.Task;
                }
                catch (Exception error) { entered.TrySetException(error); throw; }
            });
            tasks.Add(reader);
            await entered.Task.WaitAsync(Bound + Bound);
            Assert.False(reader.IsCompleted);
        }

        internal async Task ProveSharedLeasesReturnedAsync()
        {
            Assert.NotNull(reader);
            Assert.False(reader.IsCompleted);
            releaseReader.TrySetResult();
            await ObserveAsync(reader);
            // Token belongs to the actual acquisition, not only its observation.
            using var budget = new CancellationTokenSource(Bound);
            using var exclusive = await operations.EnterExclusiveAsync(budget.Token);
            Assert.False(recovery.IsBlocked);
        }

        internal async Task CleanupAsync(List<Exception> errors)
        {
            releaseReader.TrySetResult(); // Release only this test's distinct reader.
            var quiescent = true;
            foreach (var actual in tasks)
            {
                if (observed.Contains(actual)) continue;
                try { await ObserveAsync(actual); }
                catch (Exception error) { errors.Add(error); if (!actual.IsCompleted) quiescent = false; }
            }
            if (!quiescent) { Retain("Actual Frame/reader work did not terminate", errors); return; }
            if (handler is not null)
            {
                var close = connection.CompleteTransportCloseAsync();
                tasks.Add(close);
                try { await ObserveAsync(close); Assert.False(sessions.TryGet(connection, out _)); }
                catch (Exception error) { errors.Add(error); quiescent = false; }
                // Current main is not assumed to implement this. If the genuine
                // owner gains a drain API, await it rather than inventing one.
                if (quiescent && (object)handler is IAsyncDisposable disposable)
                {
                    var drain = disposable.DisposeAsync().AsTask();
                    tasks.Add(drain);
                    try { await ObserveAsync(drain); }
                    catch (Exception error) { errors.Add(error); quiescent = false; }
                }
            }
            if (!quiescent) { Retain("Transport/handler termination was not proven", errors); return; }
            observeCommittedPublication = false;
            try { if (ownsRoot) { ValidateRoot(); DeleteOrdinary(root); } }
            catch (Exception error) { errors.Add(error); }
            try { log.Dispose(); } catch (Exception error) { errors.Add(error); }
        }

        private void Retain(string reason, List<Exception> errors)
        {
            RetainedVictoryStartupFixtures.Add(this);
            errors.Add(new InvalidOperationException(reason + "; retained ownedRoot=" + root));
        }

        private void ValidateRoot()
        {
            var full = Path.GetFullPath(root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full));
            Assert.StartsWith(Prefix, Path.GetFileName(full));
            Assert.True(Guid.TryParseExact(Path.GetFileName(full)[Prefix.Length..], "N", out _));
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
                if (File.Exists(path) || Directory.Exists(path))
                    Assert.Equal(0, (int)(File.GetAttributes(path) & FileAttributes.ReparsePoint));
        }

        private void DeleteOrdinary(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                Assert.StartsWith(root + Path.DirectorySeparatorChar, Path.GetFullPath(entry), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(0, (int)(File.GetAttributes(entry) & FileAttributes.ReparsePoint));
                if (Directory.Exists(entry)) DeleteOrdinary(entry); else File.Delete(entry);
            }
            Directory.Delete(path, recursive: false);
        }
    }
}
