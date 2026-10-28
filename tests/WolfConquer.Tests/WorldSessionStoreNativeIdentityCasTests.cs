using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldSessionStoreNativeIdentityCasTests
{
    private static WorldSession CreateSession() => new(1293600, "OwnedIdentityFixture",
        new CharacterSnapshot(Uid: 1293600, Name: "OwnedIdentityFixture", Body: 1003, Face: 0, Hair: 0,
            Class: 10, FirstClass: 10, SecondClass: 0, Reborn: 0, Level: 1, Experience: 0,
            MapId: 1002, X: 300, Y: 278, PreviousMapId: 1002, PreviousMapX: 300, PreviousMapY: 278,
            Strength: 0, Agility: 0, Vitality: 0, Spirit: 0, AttributePoints: 0, MinHitPoints: 1, MinMana: 0,
            Money: 0, ConquerPoints: 0, BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1,
            SourceFile: "1293600.ini", Attributes: new Dictionary<string, string>(),
            PaidNobility: new Dictionary<string, string>()),
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);

    [Fact]
    public void ExactRecordAndIssuedIdentityAreBothRequiredIncludingRemoveReinsertAba()
    {
        var callbacks = 0;
        var store = new WorldSessionStore((_, _, _) => callbacks++);
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession();
        store.Set(connection, initial);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, initial.Character.Uid, out var identity));
        var next = initial with { ActiveNpcId = 77 };
        Assert.False(store.TryCompareAndSetWithNativeIdentity(connection, initial, new object(), next, out _));
        Assert.False(store.TryCompareAndSetWithNativeIdentity(connection, initial with { }, identity, next, out _));
        Assert.Equal(1, callbacks);
        Assert.True(store.TryRemove(connection, out var removed));
        Assert.Same(initial, removed);
        store.Set(connection, initial);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, initial.Character.Uid, out var reissued));
        Assert.NotSame(identity, reissued);
        Assert.False(store.TryCompareAndSetWithNativeIdentity(connection, initial, identity, next, out _));
        Assert.Equal(2, callbacks);
        Assert.True(store.TryCompareAndSetWithNativeIdentity(connection, initial, reissued, next, out var final));
        Assert.Same(next, final);
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(final, current);
        Assert.Equal(3, callbacks);
    }

    [Fact]
    public void PublishedFinalRemainsObservableWhenTheRealSessionObserverThrows()
    {
        var fail = false;
        var store = new WorldSessionStore((_, _, _) => { if (fail) throw new IOException("owned observer failure"); });
        var connection = new ConquerTcpConnection(null, null);
        var initial = CreateSession();
        store.Set(connection, initial);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, initial.Character.Uid, out var identity));
        var next = initial with { ActiveNpcId = 99 };
        WorldSession? final = null;
        fail = true;
        var failure = Assert.Throws<WorldSessionPublicationException>(() =>
            store.TryCompareAndSetWithNativeIdentity(connection, initial, identity, next, out final));
        Assert.IsType<IOException>(failure.InnerException);
        Assert.Equal(WorldSessionPublicationStage.Observer, failure.Stage);
        Assert.Same(next, final);
        Assert.Same(final, failure.PublishedSession);
        Assert.False(store.TryGet(connection, out _));
    }
}
