using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
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
        if (errors.Count > 1) throw new AggregateException($"Victory fixture failed; ownedRoot={fixture.Root}", errors);
    }

    private static void AddFailure(List<Exception> errors, Exception error)
    { if (!errors.Any(existing => ReferenceEquals(existing, error))) errors.Add(error); }

    private sealed partial class Fixture
    {
        private static readonly ConcurrentBag<Fixture> Retained = new();
        private readonly List<Task> calls = [];
        private readonly HashSet<Task> observedTerminals = [];
        private Task? transportClose;
        private Task? barrierProbe;
        private int retained;

        private async Task HandleAsync(PacketFrame frame)
        {
            Assert.True(recovery.IsReadyFor(Users, Items));
            Assert.False(recovery.IsBlocked);
            Assert.Contains(frame.Header.Opcode, Registry.Opcodes);
            var actual = HandleRegisteredAsync(frame);
            calls.Add(actual);
            try { await actual.WaitAsync(Bound); }
            catch (Exception observation)
            {
                if (!actual.IsCompleted) throw;
                // Observation timeout can race real completion. Observe THAT
                // SAME task before calling it terminal or releasing a leaf lease.
                try { await actual; }
                catch (Exception completion)
                {
                    observedTerminals.Add(actual);
                    if (!ReferenceEquals(observation, completion)) throw new AggregateException(observation, completion);
                    ExceptionDispatchInfo.Capture(completion).Throw();
                }
                observedTerminals.Add(actual);
                ExceptionDispatchInfo.Capture(observation).Throw();
            }
            observedTerminals.Add(actual);
        }

        private async Task HandleRegisteredAsync(PacketFrame frame)
        {
            // Genuine shared operation ownership around the complete registered
            // delegate, as in Frame dispatch. No cancellation can queue detached
            // disconnect work; the bounded waiter observes, never cancels work.
            using var operation = await operations.EnterSharedAsync(CancellationToken.None);
            var context = Context;
            Assert.Same(Connection, context.Connection);
            Assert.Same(Current, context.Session);
            await Registry.HandleAsync(context, frame, CancellationToken.None);
        }

        private async Task<bool> ObserveTerminalAsync(Task actual, List<Exception> errors)
        {
            if (observedTerminals.Contains(actual)) return true;
            try { await actual.WaitAsync(Bound); }
            catch (Exception observation)
            {
                AddFailure(errors, observation);
                if (!actual.IsCompleted) return false;
                try { await actual; }
                catch (Exception completion) { AddFailure(errors, completion); }
            }
            observedTerminals.Add(actual);
            return true;
        }

        internal async Task CleanupAsync(List<Exception> errors)
        {
            var drained = true;
            foreach (var actual in calls)
                if (!await ObserveTerminalAsync(actual, errors)) drained = false;
            if (!drained) { RetainUnknown(errors, "handler work/leaf leases"); return; }
            // Never release these real fault handles while any actual handler is
            // running, including an observation timeout. Terminal work is known.
            foreach (var lease in faultLeases.ToArray())
            {
                try { lease.Dispose(); faultLeases.Remove(lease); }
                catch (Exception error) { AddFailure(errors, error); }
            }
            if (faultLeases.Count != 0) { RetainUnknown(errors, "leaf lease release"); return; }
            if (operations is not null)
            {
                barrierProbe ??= ProbeReleasedBarrierAsync();
                if (!await ObserveTerminalAsync(barrierProbe, errors) || !barrierProbe.IsCompletedSuccessfully)
                { RetainUnknown(errors, "operation lease release"); return; }
            }
            transportClose ??= Connection.CompleteTransportCloseAsync();
            if (!await ObserveTerminalAsync(transportClose, errors) || !transportClose.IsCompletedSuccessfully)
            { RetainUnknown(errors, "transport close"); return; }
            // No Frame handler/runtime/Host is created. Only actual registered
            // work, barrier leases and the real physical connection were owned.
            try { if (ownsRoot) { ValidateRoot(); DeleteOrdinaryTree(Root); } }
            catch (Exception error) { AddFailure(errors, error); }
            try { Log.Dispose(); }
            catch (Exception error) { AddFailure(errors, error); }
        }

        private async Task ProbeReleasedBarrierAsync()
        { using var operation = await operations.EnterExclusiveAsync(CancellationToken.None); }

        private void RetainUnknown(List<Exception> errors, string boundary)
        {
            Retain();
            AddFailure(errors, new InvalidOperationException($"Victory fixture retained for unproven {boundary}; ownedRoot={Root}"));
        }

        internal void Retain()
        { if (Interlocked.Exchange(ref retained, 1) == 0) Retained.Add(this); }

        private void ValidateRoot()
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root));
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(RootPrefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[RootPrefix.Length..], "N", out _))
                throw new InvalidOperationException("Not the exact owned Victory root.");
            for (string? path = full; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Victory fixture has a reparse ancestor.");
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
                    throw new InvalidOperationException("Cleanup entry is outside the exact ordinary Victory root.");
                if (Directory.Exists(entry)) DeleteOrdinaryTree(entry); else File.Delete(entry);
            }
            Directory.Delete(directory, recursive: false);
        }
    }
}
