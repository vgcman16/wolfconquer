using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionPublicationObserverTests
{
    [Fact]
    public void ObserverMayRetireAndReissueAfterCasWithoutChangingItsCommittedResult()
    {
        var connection = new ConquerTcpConnection(null, null);
        bool observe = false;
        WorldSession? observed = null;
        var original = Session();
        var proposed = original with { Username = "CommittedOwner" };
        var replacement = original with { Username = "ReplacementOwner" };
        WorldSessionStore? store = null;
        store = new((actual, before, current) =>
        {
            if (!observe) return;
            observe = false;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.Same(connection, actual); Assert.Same(original, before);
            observed = current;
            Assert.True(store!.TryRemove(actual, out var removed)); Assert.Same(current, removed);
            store.Set(actual, replacement);
        });
        store.Set(connection, original);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, original.Character.Uid, out var identity));
        observe = true;

        Assert.True(store.TryCompareAndSet(connection, original, proposed, out var committed));

        Assert.Same(proposed, committed); Assert.Same(committed, observed);
        Assert.True(store.TryGet(connection, out var actualCurrent)); Assert.Same(replacement, actualCurrent);
        Assert.False(LegacyNativeClientPublication.TryCurrent(store, connection, original.Character.Uid, identity, out _));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Fact]
    public void ObserverCanUpdateAnotherHolderAfterThePublishingGateIsReleased()
    {
        var first = new ConquerTcpConnection(null, null);
        var second = new ConquerTcpConnection(null, null);
        bool observe = false;
        var other = Session(1293601);
        var replacement = other with { Username = "OtherCommittedOwner" };
        WorldSessionStore? store = null;
        store = new((actual, _, _) =>
        {
            if (!observe || !ReferenceEquals(actual, first)) return;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.True(store!.TryCompareAndSet(second, other, replacement));
        });
        store.Set(first, Session()); store.Set(second, other);
        Assert.True(store.TryGet(first, out var current));
        observe = true;

        store.Set(first, current! with { Username = "FirstCommittedOwner" });

        Assert.True(store.TryGet(second, out var actualOther)); Assert.Same(replacement, actualOther);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Fact]
    public void PreparationCallbackStillCannotRetireTheCurrentHolder()
    {
        var connection = new ConquerTcpConnection(null, null);
        int notifications = 0;
        var store = new WorldSessionStore((_, _, _) => notifications++);
        var original = Session(); store.Set(connection, original); notifications = 0;
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, original.Character.Uid, out var identity));

        Assert.Throws<InvalidOperationException>(() => store.TryUpdateCurrentNativeSession(
            connection, original.Character.Uid, identity, current =>
            {
                store.TryRemove(connection, out _);
                return current with { Username = "MustNotPublish" };
            }, out _));

        Assert.True(store.TryGet(connection, out var actual)); Assert.Same(original, actual);
        Assert.Equal(0, notifications);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Fact]
    public void ObserverExceptionRetainsTheActualCommittedResultAndReleasesTheGate()
    {
        var connection = new ConquerTcpConnection(null, null);
        bool observe = false;
        var failure = new InvalidOperationException("owned observer failure");
        var store = new WorldSessionStore((_, _, _) =>
        {
            if (!observe) return;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            throw failure;
        });
        var original = Session(); store.Set(connection, original);
        var proposed = original with { Username = "CommittedDespiteObserverFailure" };
        WorldSession? final = null; observe = true;

        var actualFailure = Assert.Throws<WorldSessionPublicationException>(() =>
            store.TryCompareAndSet(connection, original, proposed, out final));

        Assert.Same(failure, actualFailure.InnerException); Assert.Same(proposed, final);
        Assert.Same(final, actualFailure.PublishedSession);
        Assert.Equal(WorldSessionPublicationStage.Observer, actualFailure.Stage);
        Assert.False(store.TryGet(connection, out _));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    private static WorldSession Session(uint uid = 1293600) => new(uid, "OriginalOwner",
        CharacterSnapshotParser.ParseText($"""
            [Character]
            UID={uid}
            Name=ObserverOwner
            Body=1008
            Face=0
            Haire=0
            Class=15
            FirstClass=15
            SecoundeClass=0
            Reborn=0
            Level=110
            Experience=0
            Map=1002
            X=430
            Y=378
            PMap=1002
            PMapX=430
            PMapY=378
            Strength=0
            Agility=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=5000
            MinMana=300
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=1
            """),
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
}
