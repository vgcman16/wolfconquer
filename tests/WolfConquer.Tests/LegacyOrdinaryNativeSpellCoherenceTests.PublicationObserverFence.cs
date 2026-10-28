using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public Task ObserverFenceGateOrderFailurePreservesActualTypedPublishedOutcome() => RunAsync(async f =>
    {
        // The producer comes from the real owned registry, domain and recovery
        // fixture. The detached Store below is an ordinary managed Store, not a
        // substitute producer or accepting persistence callback.
        await f.LoginAsync();
        var producer = Assert.IsType<LegacyMailboxPackageProducer>(f.PackageProducer);
        var session = f.Current with { NativeGameClient = null, UsersRoot = null };
        var connection = new ConquerTcpConnection(null, null);
        bool fail = false;
        var cause = new IOException("actual observer failure");
        var store = new WorldSessionStore((_, _, _) => { if (fail) throw cause; });
        store.Set(connection, session);
        var proposed = session with { ActiveNpcId = 41 };
        fail = true;
        var published = Assert.Throws<WorldSessionPublicationException>(() => store.Set(connection, proposed));
        Assert.Same(proposed, published.PublishedSession);
        Assert.Same(cause, published.InnerException);
        Assert.Equal(WorldSessionPublicationStage.Observer, published.Stage);
        Assert.Null(published.FencingFailure);
        Assert.False(store.TryGet(connection, out _));

        fail = false;
        var reissued = proposed with { ActiveNpcId = 42 };
        store.Set(connection, reissued);
        var method = typeof(WorldSessionStore).GetMethod("FenceObserverFailure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        // Exercise the same real fencing routine while a real Store identity
        // gate is held, then attach the already-created genuine package owner.
        // EnterStoreScope must reject this actual order rather than invert it.
        Assert.True(store.WithCurrentNativeClientIdentity(connection, reissued.Character.Uid, _ =>
        {
            store.BindMailboxPackages(producer);
            method!.Invoke(store, [published]);
        }));
        var secondary = Assert.IsType<InvalidOperationException>(published.FencingFailure);
        Assert.Contains("Package Store mutation gate order is invalid", secondary.Message);
        Assert.Same(cause, published.InnerException);
        Assert.Equal(WorldSessionPublicationStage.Observer, published.Stage);
        Assert.Same(proposed, published.PublishedSession);
        Assert.True(store.TryGet(connection, out var current)); Assert.Same(reissued, current);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }, combined: true);
}
