using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static readonly System.Collections.Concurrent.ConcurrentBag<PackageJoin> UnfinishedPackageJoins = [];
    private static async Task RunPackageJoinAsync(Func<Fixture, PackageJoin, Task> body, long job = 10,
        uint flag = 1, int inventoryCount = 1, string? catalogFault = null, bool bindProducer = true,
        bool nativeComponent = false)
    {
        var fixture = new Fixture();
        PackageJoin? join = null;
        var errors = new List<Exception>();
        try
        {
            fixture.SeedMailboxPackageJoin(job, flag, inventoryCount);
            if (nativeComponent && job == 5051) fixture.SeedPackageJoinNinjaLine();
            join = new PackageJoin(fixture, PackageJoinCatalog(catalogFault), bindProducer, nativeComponent ? job : null);
            await join.StartAsync(bootstrap: true);
            await body(fixture, join);
        }
        catch (Exception error) { errors.Add(error); }
        if (join is not null)
            try { await join.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (join is null || join.IsClosed)
        {
            try { await fixture.CleanupAsync(errors); }
            catch (Exception error) { errors.Add(error); Unfinished.Add(fixture); }
        }
        else
        {
            Unfinished.Add(fixture);
            errors.Add(new InvalidOperationException($"Package fixture retained because real owner closure is unproven; ownedRoot={fixture.Root}."));
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Package join operation and cleanup failed.", errors);
    }

    private sealed partial class PackageJoin(Fixture fixture, WorldDataCatalog worldData, bool bindProducer,
        long? nativeComponentJob = null) : IAsyncDisposable
    {
        internal LegacyItemUidRegistry Registry { get; private set; } = null!;
        internal Guid Domain { get; private set; }
        internal LegacyTradeRecoveryState Recovery { get; private set; } = null!;
        internal WorldServerFrameHandler? Handler { get; private set; }
        internal ConquerTcpConnection? Connection { get; private set; }
        internal Action<byte[]>? SendObserver { get; set; }
        internal bool IsClosed { get; private set; }
        internal WorldSession Current => Assert.IsType<WorldSession>(
            SessionStore.TryGet(Connection!, out var session) ? session : null);
        internal WorldSessionStore SessionStore
        {
            get
            {
                if (componentContext is not null) return componentContext.Sessions!;
                Assert.NotNull(Handler);
                var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
                return Assert.IsType<WorldSessionStore>(field!.GetValue(Handler)); // Read actual store; never inject owners.
            }
        }

        internal async Task StartAsync(bool bootstrap)
        {
            IsClosed = false;
            Recovery = new(fixture.Users, fixture.Items, null, fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(Recovery.IsReadyForMailboxGear(fixture.Users, fixture.Items, fixture.PackageMailboxRoot, fixture.PackageGearRoot));
            var opened = bootstrap
                ? LegacyItemUidRegistry.TryBootstrap(fixture.PackageRegistryOptions(), out var owner, out reason)
                : LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out owner, out reason);
            Assert.True(opened, reason);
            Registry = Assert.IsType<LegacyItemUidRegistry>(owner);
            Domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(fixture.PackageUidState))!["Domain"]!.GetValue<string>(), "N");
            Assert.True(LegacyMailboxPackageProducer.TryCreate(Registry, Domain, Recovery, fixture.Users, fixture.Items,
                fixture.PackageMailboxRoot, fixture.PackageGearRoot, out var producer, out reason), reason);
            if (nativeComponentJob.HasValue)
            {
                StartNativeComponent(producer!);
                return;
            }
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
                playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore(), lifetime: TimeSpan.FromMinutes(1));
            Handler = bindProducer
                ? new WorldServerFrameHandler(loader, handoff, worldData, producer!, fixture.Log, ConquerProtocolProfile.VisibleDx8D13)
                : new WorldServerFrameHandler(loader, handoff, worldData, LegacyRuntimeUpdateLoopOptions.Disabled,
                    log: fixture.Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                var bytes = frame.ToArray();
                fixture.Sent.Add(bytes);
                SendObserver?.Invoke(bytes);
                return ValueTask.CompletedTask;
            });
            var ticket = handoff.Issue(fixture.Account, "mailbox-package-join", ConquerRuntimeDefaults.WorldServer);
            await FrameAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes());
            await FrameAsync(new MsgActionPacket { ObjId = Fixture.Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            Assert.Equal(fixture.Account, Current.AccountId);
            Assert.Equal(Fixture.Uid, Current.Character.Uid);
            Assert.Null(Connection.NativeGameClient);
        }

        internal Task FrameAsync(byte[] bytes)
        {
            var actual = componentContext is null
                ? Handler!.HandleFrameAsync(Connection!, ReadFrame(bytes), CancellationToken.None).AsTask()
                : ComponentRegistry.HandleAsync(componentContext with { Session = Current }, ReadFrame(bytes), CancellationToken.None).AsTask();
            fixture.Calls.Add(actual);
            return fixture.ObserveAsync(actual);
        }

        internal async Task RestartAsync()
        {
            await DisposeAsync();
            await StartAsync(bootstrap: false);
        }

        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            var terminal = true;
            foreach (var actual in fixture.Calls.ToArray().Where(actual => !actual.IsCompleted))
            {
                try { await fixture.ObserveAsync(actual); }
                catch (Exception error) { errors.Add(error); if (!actual.IsCompleted) terminal = false; }
            }
            if (!terminal) { RetainPackageJoin(errors); return; }
            SendObserver = null;
            var connection = Connection;
            var handler = Handler;
            if (componentContext is { } context && connection is not null)
            {
                if (context.Sessions!.TryRemove(connection, out var retired) && retired is not null)
                    LegacySecondaryPasswordRuntime.Invalidate(connection, retired);
                componentContext = null;
            }
            if (connection is not null)
            {
                var close = connection.CompleteTransportCloseAsync();
                fixture.Calls.Add(close);
                try { await fixture.ObserveAsync(close); Connection = null; }
                catch (Exception error) { errors.Add(error); terminal = false; }
            }
            if (terminal && (object?)handler is IAsyncDisposable drainOwner)
            {
                var drain = drainOwner.DisposeAsync().AsTask();
                fixture.Calls.Add(drain);
                try { await fixture.ObserveAsync(drain); Handler = null; }
                catch (Exception error) { errors.Add(error); terminal = false; }
            }
            if (!terminal) { RetainPackageJoin(errors); return; }
            Handler = null;
            try { Registry?.Dispose(); IsClosed = true; } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Package join owners failed to close.", errors);
        }

        private void RetainPackageJoin(List<Exception> errors)
        {
            UnfinishedPackageJoins.Add(this);
            errors.Add(new InvalidOperationException($"Package join retained real UID lease; closure unproven; ownedRoot={fixture.Root}."));
            throw new AggregateException("Package join cleanup retained unfinished owners.", errors);
        }
    }
}
