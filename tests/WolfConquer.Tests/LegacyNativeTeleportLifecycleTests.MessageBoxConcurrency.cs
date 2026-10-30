using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public async Task ParallelAcceptedRepliesCanClaimTheSameInstalledPairOnlyOnce()
    {
        using var owned = await CreateMessageBoxConcurrencyInputs(4);
        var scope = owned.Scope; scope.Client.Player.Map = 1002;
        var deadline = DateTime.Now.AddDays(1); int calls = 0, cancelCalls = 0;
        scope.Client.Player.SetMessageBox(user =>
        {
            Assert.Same(scope.Client, user); Interlocked.Increment(ref calls);
        }, _ => Interlocked.Increment(ref cancelCalls), deadline);
        var replies = owned.Replies;
        using var start = new Barrier(replies.Length);
        Task[] workers = replies.Select(reply => MessageBoxWorker(() =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("message-box workers did not rendezvous");
            LegacyNativeMsgStaticMessage.Handler(scope.Client, reply);
        })).ToArray();
        await Task.WhenAll(workers);
        Assert.Equal(1, calls); Assert.Equal(0, cancelCalls);
        Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
        Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
        foreach (var reply in replies)
        {
            Assert.Equal(8, reply.Position); Assert.Equal(8, reply.Size);
            Assert.Equal(Convert.FromHexString("0800100901000000"), Bytes(reply));
        }
    }

    [Fact]
    public async Task ReplacementCanInstallOnAnotherThreadWhileTheClaimedCallbackIsStillRunning()
    {
        using var owned = await CreateMessageBoxConcurrencyInputs(1);
        var scope = owned.Scope; var reply = owned.Replies[0];
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var installed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(); scope.Client.Player.Map = 1002;
        var newDeadline = DateTime.Now.AddDays(2); int oldCalls = 0;
        Action<LegacyNativeGameClientRuntime> replacement = _ => throw new Exception("replacement has no reply yet");
        Action<LegacyNativeGameClientRuntime> cancel = _ => throw new Exception("replacement has no reply yet");
        scope.Client.Player.SetMessageBox(user =>
        {
            Assert.Same(scope.Client, user); Interlocked.Increment(ref oldCalls); entered.TrySetResult(true);
            if (!release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("owned callback was not released");
        }, null, DateTime.Now.AddDays(1));
        Task response = MessageBoxWorker(() => LegacyNativeMsgStaticMessage.Handler(scope.Client, reply));
        Task? publisher = null; bool enteredInTime = false, installedInTime = false;
        try
        {
            enteredInTime = await ObserveMessageBoxSignal(entered.Task);
            if (enteredInTime)
            {
                publisher = MessageBoxWorker(() =>
                {
                    try { scope.Client.Player.SetMessageBox(replacement, cancel, newDeadline); }
                    finally { installed.TrySetResult(true); }
                });
                installedInTime = await ObserveMessageBoxSignal(installed.Task);
            }
        }
        finally
        {
            release.Set();
            Task[] workers = publisher is null ? [response] : [response, publisher];
            await Task.WhenAll(workers);
        }
        Assert.True(enteredInTime); Assert.True(installedInTime);
        Assert.Equal(1, oldCalls); Assert.Same(replacement, scope.Client.Player.MessageOK);
        Assert.Same(cancel, scope.Client.Player.MessageCancel); Assert.Equal(newDeadline, scope.Client.Player.StartMessageBox);
        Assert.Equal(8, reply.Position); Assert.Empty(scope.Queue);
    }

    [Fact]
    public async Task ExpiredCleanupRacingAtomicInstallationCannotSilentlyEraseTheNewPair()
    {
        using var owned = await CreateMessageBoxConcurrencyInputs(1);
        var scope = owned.Scope; var reply = owned.Replies[0];
        using var start = new Barrier(2); scope.Client.Player.Map = 1002;
        int expiredCalls = 0, newCalls = 0, cancelCalls = 0; var deadline = DateTime.Now.AddDays(1);
        Action<LegacyNativeGameClientRuntime> replacement = user =>
        {
            Assert.Same(scope.Client, user); Interlocked.Increment(ref newCalls);
        };
        Action<LegacyNativeGameClientRuntime> cancel = _ => Interlocked.Increment(ref cancelCalls);
        scope.Client.Player.SetMessageBox(_ => Interlocked.Increment(ref expiredCalls),
            _ => Interlocked.Increment(ref expiredCalls), DateTime.Now.AddDays(-1));
        void Rendezvous()
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("expired-box workers did not rendezvous");
        }
        Task response = MessageBoxWorker(() => { Rendezvous(); LegacyNativeMsgStaticMessage.Handler(scope.Client, reply); });
        Task publisher = MessageBoxWorker(() => { Rendezvous(); scope.Client.Player.SetMessageBox(replacement, cancel, deadline); });
        await Task.WhenAll(response, publisher);
        Assert.Equal(0, expiredCalls); Assert.Equal(0, cancelCalls); Assert.InRange(newCalls, 0, 1);
        if (newCalls == 0)
        {
            Assert.Same(replacement, scope.Client.Player.MessageOK); Assert.Same(cancel, scope.Client.Player.MessageCancel);
        }
        else
        {
            // The wire has no box ID: installation may win, so this reply can
            // legitimately claim the current new pair. It must never erase it silently.
            Assert.Null(scope.Client.Player.MessageOK); Assert.Null(scope.Client.Player.MessageCancel);
        }
        Assert.Equal(deadline, scope.Client.Player.StartMessageBox); Assert.Empty(scope.Queue);
    }

    private static async Task<bool> ObserveMessageBoxSignal(Task signal)
    {
        try { await signal.WaitAsync(TimeSpan.FromSeconds(5)); return true; }
        catch (TimeoutException) { return false; }
    }

    private static async Task<MessageBoxConcurrencyInputs> CreateMessageBoxConcurrencyInputs(int replyCount)
    {
        MessageBoxConcurrencyInputs? owned = null;
        // Keep genuine native constructors on the fresh stack required by Packet;
        // asynchronous orchestration and awaiting workers stay outside that action.
        await NativePacketTestThread.Run(() => owned = new MessageBoxConcurrencyInputs(replyCount));
        return owned ?? throw new InvalidOperationException("Native message-box inputs were not created.");
    }

    private sealed class MessageBoxConcurrencyInputs : IDisposable
    {
        internal Scope Scope { get; } = new();
        internal LegacyNativePacket[] Replies { get; }

        internal MessageBoxConcurrencyInputs(int replyCount)
        {
            var packets = new List<LegacyNativePacket>();
            try
            {
                for (int index = 0; index < replyCount; index++) packets.Add(IndependentMessageBoxReply());
                Replies = packets.ToArray();
            }
            catch (Exception primary)
            {
                try
                {
                    foreach (var packet in packets) packet.Dispose();
                    Scope.Dispose();
                }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
        }

        public void Dispose()
        {
            foreach (var reply in Replies) reply.Dispose();
            Scope.Dispose();
        }
    }

    private static Task MessageBoxWorker(Action body) => Task.Factory.StartNew(body,
        CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}
