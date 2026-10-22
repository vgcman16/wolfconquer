using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldSessionStoreCompleteLoginTests
{
    private const uint Uid = 1_299_701;

    [Fact]
    public void CompletionUsesLatestSessionAndPublishesPhysicalIssuerBeforeNormalCallback()
    {
        var (connection, original, native) = BoundSession();
        var callbacks = new List<(WorldSession? Before, WorldSession After)>();
        var observeCompletion = false;
        WorldSessionStore? store = null;
        store = new WorldSessionStore((seenConnection, before, after) =>
        {
            callbacks.Add((before, after));
            if (!observeCompletion) return;
            Assert.Same(connection, seenConnection);
            Assert.True(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.True(native.Player.CompleteLogin);
            Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, after));
            Assert.True(store!.TryGet(connection, out var published));
            Assert.Same(after, published);
        });
        store.Set(connection, original);
        var owner = Capture(connection, original);
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.Ordinal)
        { ["Money"] = "7777", ["ConquerPoints"] = "111", ["AwaitedChange"] = "retained", ["completelogin"] = "copied" };
        var items = new PlayerItemSnapshot([new PlayerItemSnapshotItem { Uid = 9001, ItemId = 50000 }], [], [], [], []);
        var latest = original with
        {
            Character = original.Character with { Money = 7777, ConquerPoints = 111, X = 444, Attributes = attrs },
            PlayerItems = items, ActiveNpcId = 20499,
        };
        store.Set(connection, latest);
        callbacks.Clear();
        observeCompletion = true;

        Assert.True(store.TryCompleteLegacyLogin(connection, original, owner, out var updated));

        var current = Assert.IsType<WorldSession>(updated);
        Assert.Equal(7777, current.Character.Money);
        Assert.Equal(111, current.Character.ConquerPoints);
        Assert.Equal(444, current.Character.X);
        Assert.Equal("retained", current.Character.Attributes["AwaitedChange"]);
        Assert.Equal("1", current.Character.Attributes["CompleteLogin"]);
        Assert.Single(current.Character.Attributes, pair => pair.Key.Equals("CompleteLogin", StringComparison.OrdinalIgnoreCase));
        Assert.Same(items, current.PlayerItems);
        Assert.Equal((uint?)20499, current.ActiveNpcId);
        var callback = Assert.Single(callbacks);
        Assert.Same(latest, callback.Before);
        Assert.Same(current, callback.After);
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrRemovedHolderCannotBeResurrectedByCompletion(bool removed)
    {
        var (connection, original, native) = BoundSession();
        var store = new WorldSessionStore();
        var owner = Capture(connection, original);
        if (removed)
        {
            store.Set(connection, original);
            Assert.True(store.TryRemove(connection, out var prior));
            Assert.Same(original, prior);
        }
        Assert.False(store.TryCompleteLegacyLogin(connection, original, owner, out var updated));
        Assert.Null(updated);
        Assert.False(store.TryGet(connection, out _));
        Assert.Empty(store.Snapshot());
        Assert.False(native.Player.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, original));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedAccountOrCharacterUidCannotCompleteAnOldStartup(bool changeUid)
    {
        var (connection, original, native) = BoundSession();
        var store = new WorldSessionStore();
        store.Set(connection, original);
        var owner = Capture(connection, original);
        var latest = changeUid ? original with { Character = original.Character with { Uid = Uid + 1 } }
            : original with { AccountId = original.AccountId + 1 };
        store.Set(connection, latest);
        Assert.False(store.TryCompleteLegacyLogin(connection, original, owner, out var updated));
        Assert.Null(updated);
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(latest, current);
        Assert.False(native.Player.CompleteLogin);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacedPhysicalPlayerOrSessionClientCannotCompleteCapturedOwner(bool replaceClient)
    {
        var (connection, original, native) = BoundSession();
        var store = new WorldSessionStore();
        store.Set(connection, original);
        var owner = Capture(connection, original);
        var priorPlayer = native.Player;
        var latest = original;
        if (replaceClient)
        {
            var foreign = new LegacyNativeGameClientRuntime();
            foreign.Player.UID = Uid;
            latest = original with { NativeGameClient = foreign };
            store.Set(connection, latest);
        }
        else native.Player = new LegacyNativePlayerRuntime(native) { UID = Uid };
        Assert.False(store.TryCompleteLegacyLogin(connection, original, owner, out var updated));
        Assert.Null(updated);
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(latest, current);
        Assert.False(priorPlayer.CompleteLogin);
        Assert.False(native.Player.CompleteLogin);
    }

    [Fact]
    public void OwnerCapturedForAnotherConnectionCannotCompleteTheSameUid()
    {
        var (connection, original, native) = BoundSession();
        var foreignConnection = new ConquerTcpConnection(null, null, nativeGameClient: native);
        var foreignOwner = Capture(foreignConnection, original);
        var store = new WorldSessionStore();
        store.Set(connection, original);
        Assert.False(store.TryCompleteLegacyLogin(connection, original, foreignOwner, out var updated));
        Assert.Null(updated);
        Assert.False(native.Player.CompleteLogin);
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(original, current);
    }

    [Fact]
    public void ValidCurrentOwnerCannotCompleteAMismatchedOriginalPhysicalClientWithTheSameNumericIdentity()
    {
        var (connection, current, native) = BoundSession();
        var foreign = new LegacyNativeGameClientRuntime();
        foreign.Player.UID = Uid;
        var original = current with { NativeGameClient = foreign };
        var owner = Capture(connection, current);
        var store = new WorldSessionStore();
        store.Set(connection, current);
        Assert.False(store.TryCompleteLegacyLogin(connection, original, owner, out var updated));
        Assert.Null(updated);
        Assert.True(store.TryGet(connection, out var retained));
        Assert.Same(current, retained);
        Assert.False(native.Player.CompleteLogin);
        Assert.False(foreign.Player.CompleteLogin);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NullOwnerCannotBypassAnyPhysicalConnectionOriginalOrCurrentClient(int boundPart)
    {
        var (_, bound, native) = BoundSession();
        var unbound = bound with { NativeGameClient = null };
        var connection = new ConquerTcpConnection(null, null, nativeGameClient: boundPart == 0 ? native : null);
        var original = boundPart == 1 ? bound : unbound;
        var current = boundPart == 2 ? bound : unbound;
        var store = new WorldSessionStore();
        store.Set(connection, current);
        Assert.False(store.TryCompleteLegacyLogin(connection, original, owner: null, out var updated));
        Assert.Null(updated);
        Assert.True(store.TryGet(connection, out var retained));
        Assert.Same(current, retained);
        Assert.False(native.Player.CompleteLogin);
    }

    [Fact]
    public void WhollyUnboundCompatibilityProjectsCompletionWithoutFabricatingNativeOwner()
    {
        var original = Session();
        var connection = new ConquerTcpConnection(null, null);
        var store = new WorldSessionStore();
        store.Set(connection, original);
        var latest = original with { Character = original.Character with { Money = 5555 } };
        store.Set(connection, latest);
        Assert.True(store.TryCompleteLegacyLogin(connection, original, owner: null, out var updated));
        var current = Assert.IsType<WorldSession>(updated);
        Assert.Equal(5555, current.Character.Money);
        Assert.Equal("1", current.Character.Attributes["CompleteLogin"]);
        Assert.Null(connection.NativeGameClient);
        Assert.Null(current.NativeGameClient);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, current));
    }

    [Fact]
    public void CallbackExceptionRetainsSetPublicationSemanticsAndReleasesNativeGateMarker()
    {
        var (connection, original, native) = BoundSession();
        var store = new WorldSessionStore((_, before, _) =>
        {
            if (before is not null) throw new IOException("Injected ordinary callback failure.");
        });
        store.Set(connection, original);
        var owner = Capture(connection, original);
        WorldSession? attempted = null;
        Assert.Throws<IOException>(() => store.TryCompleteLegacyLogin(connection, original, owner, out attempted));
        Assert.True(store.TryGet(connection, out var current));
        Assert.Same(attempted, current);
        Assert.Equal("1", current!.Character.Attributes["CompleteLogin"]);
        Assert.True(native.Player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, current));
        Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
    }

    private static LegacyCompleteLoginRuntime.Owner Capture(ConquerTcpConnection connection, WorldSession session)
    {
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var owner));
        Assert.NotNull(owner);
        return owner;
    }

    private static (ConquerTcpConnection Connection, WorldSession Session, LegacyNativeGameClientRuntime Client) BoundSession()
    {
        var native = new LegacyNativeGameClientRuntime();
        native.Player.UID = Uid;
        return (new(null, null, nativeGameClient: native), Session() with { NativeGameClient = native }, native);
    }

    private static WorldSession Session() => new(Uid, "complete-login-fixture",
        new CharacterSnapshot(Uid: Uid, Name: "Completion", Body: 1003, Face: 0, Hair: 0, Class: 10,
            FirstClass: 10, SecondClass: 0, Reborn: 0, Level: 1, Experience: 0, MapId: 1002, X: 410, Y: 350,
            PreviousMapId: 1002, PreviousMapX: 410, PreviousMapY: 350, Strength: 0, Agility: 0, Vitality: 0,
            Spirit: 0, AttributePoints: 0, MinHitPoints: 1, MinMana: 0, Money: 1000, ConquerPoints: 50,
            BoundConquerPoints: 0, BankConquerPoints: 0, PkPoints: 0, Online: 1, SourceFile: "fixture.ini",
            Attributes: new Dictionary<string, string> { ["Money"] = "1000", ["CompleteLogin"] = "0" },
            PaidNobility: new Dictionary<string, string>()),
        PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
}
