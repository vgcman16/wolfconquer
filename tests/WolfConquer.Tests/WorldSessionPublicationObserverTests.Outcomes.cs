using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionPublicationObserverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObserverFailureNeverClearsAGenuinelyReissuedHolderEvenWithTheSameRecord(bool sameRecord)
    {
        var connection = new ConquerTcpConnection(null, null);
        var original = Session();
        var proposed = original with { Username = "PublishedFirst" };
        var replacement = sameRecord ? proposed : original with { Username = "ReissuedForeignHolder" };
        object? reissuedIdentity = null;
        bool observe = false;
        var cause = new IOException("actual postpublication observer failure");
        WorldSessionStore? store = null;
        store = new((actual, _, current) =>
        {
            if (!observe) return;
            observe = false;
            Assert.Same(proposed, current);
            Assert.True(store!.TryRemove(actual, out var removed)); Assert.Same(proposed, removed);
            store.Set(actual, replacement);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, actual, original.Character.Uid,
                out reissuedIdentity));
            throw cause;
        });
        store.Set(connection, original);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, original.Character.Uid, out var identity));
        WorldSession? final = null; observe = true;

        var failure = Assert.Throws<WorldSessionPublicationException>(() =>
            store.TryCompareAndSet(connection, original, proposed, out final));

        Assert.Same(proposed, final); Assert.Same(final, failure.PublishedSession);
        Assert.Same(cause, failure.InnerException); Assert.Same(identity, failure.NativeIdentity);
        Assert.Equal(WorldSessionPublicationStage.Observer, failure.Stage);
        Assert.NotSame(identity, reissuedIdentity);
        Assert.True(store.TryGet(connection, out var current)); Assert.Same(replacement, current);
        Assert.True(LegacyNativeClientPublication.TryCurrent(store, connection, original.Character.Uid,
            reissuedIdentity!, out var actual)); Assert.Same(replacement, actual);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Fact]
    public void ObserverFailureFencesALaterRecordOnTheOriginalIssuance()
    {
        var connection = new ConquerTcpConnection(null, null);
        var original = Session(); var proposed = original with { ActiveNpcId = 2 };
        var later = proposed with { Username = "SameIssuanceLaterRecord" };
        bool observe = false;
        WorldSessionStore? store = null;
        store = new((actual, _, _) =>
        {
            if (!observe) return;
            observe = false; store!.Set(actual, later);
            throw new IOException("observer failed after a same-issuance record update");
        });
        store.Set(connection, original); observe = true;

        var failure = Assert.Throws<WorldSessionPublicationException>(() => store.Set(connection, proposed));

        Assert.Same(proposed, failure.PublishedSession);
        Assert.False(store.TryGet(connection, out _));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Fact]
    public void NativeCurrentUpdateReturnsPublishedRecordThroughTypedObserverFailure()
    {
        var connection = new ConquerTcpConnection(null, null);
        var original = Session(); bool observe = false;
        var cause = new IOException("update observer failed");
        var store = new WorldSessionStore((_, _, _) => { if (observe) throw cause; });
        store.Set(connection, original);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, original.Character.Uid, out var identity));
        var proposed = original with { ActiveNpcId = 9 };
        WorldSession? final = null; observe = true;

        var failure = Assert.Throws<WorldSessionPublicationException>(() =>
            store.TryUpdateCurrentNativeSession(connection, original.Character.Uid, identity, _ => proposed, out final));

        Assert.Same(proposed, final); Assert.Same(final, failure.PublishedSession);
        Assert.Same(cause, failure.InnerException); Assert.Equal(WorldSessionPublicationStage.Observer, failure.Stage);
        Assert.False(store.TryGet(connection, out _));
    }

    [Fact]
    public void ActualFramePersistenceFailureIsPublishedMemoryWithItsDistinctPhaseAndRealRecovery()
    {
        using var log = new FailingPublicationLog();
        var frame = new WorldServerFrameHandler(snapshotLoader: null, log: log);
        var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(frame));
        var recovery = Assert.IsType<LegacyTradeRecoveryState>(typeof(WorldServerFrameHandler)
            .GetField("_tradeRecovery", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(frame));
        var connection = new ConquerTcpConnection(null, null);
        var original = Session(); store.Set(connection, original);
        var proposed = original with { PlayerSpells = new([new(1195, 0, 7, 0, 0, 0)]) };
        log.Armed = true;
        WorldSession? final = null;

        var failure = Assert.Throws<WorldSessionPublicationException>(() =>
            store.TryCompareAndSet(connection, original, proposed, out final));

        Assert.Same(proposed, final); Assert.Same(final, failure.PublishedSession);
        Assert.Equal(WorldSessionPublicationStage.FramePersistence, failure.Stage);
        Assert.Same(log.Cause, failure.InnerException);
        Assert.True(recovery.IsBlocked); Assert.False(store.TryGet(connection, out _));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
        // There is no saved-root owner here: a published snapshot alone is
        // deliberately not asserted to be a successful durable file write.
    }

    private sealed class FailingPublicationLog : StringWriter
    {
        internal bool Armed;
        internal IOException Cause { get; } = new("actual Frame persistence diagnostic route failed");
        public override void WriteLine(string? value)
        {
            if (Armed) throw Cause;
            base.WriteLine(value);
        }
    }
}
