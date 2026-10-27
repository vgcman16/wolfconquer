using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    [Theory]
    [MemberData(nameof(InvalidProvidedRoots))]
    public void InvalidProvidedRootCannotPublishThroughSetOrCompareAndSetWithUnrelatedRealOwners(string invalidRoot) =>
        WithRegisteredOwners(owners =>
        {
            var files = owners.Disk.Files();
            foreach (var bindProducer in new[] { false, true })
            {
                var changes = 0;
                var store = new WorldSessionStore((_, _, _) => changes++);
                if (bindProducer) store.BindMailboxPackages(owners.Producer);
                var connection = new ConquerTcpConnection(null, null);
                var current = owners.Session();
                store.Set(connection, current);
                changes = 0;
                var invalid = current with
                {
                    UsersRoot = invalidRoot,
                    Character = current.Character with { Money = current.Character.Money + 1 },
                };

                AssertInvalidRoot(() => store.Set(connection, invalid));
                AssertInvalidRoot(() => store.TryCompareAndSet(connection, current, invalid));
                AssertInvalidRoot(() => store.TryCompareAndSet(connection, invalid, current));
                var unownedConnection = new ConquerTcpConnection(null, null);
                AssertInvalidRoot(() => store.Set(unownedConnection, invalid));

                AssertCurrent(store, connection, current);
                Assert.False(store.TryGet(unownedConnection, out _));
                Assert.Single(store.Snapshot());
                Assert.Equal(0, changes);
            }
            owners.Disk.AssertFiles(files);
        });

    [Theory]
    [InlineData("null")]
    [InlineData("relative")]
    [InlineData("absolute")]
    public void ExplicitStorageFreeAndValidUnrelatedRootsRetainSetCompareAndSetAndRemovalBehavior(string route) =>
        WithRegisteredOwners(owners =>
        {
            var supplied = route switch
            {
                "null" => null,
                "relative" => Path.Combine(".", "wolf-unrelated-session-" + Guid.NewGuid().ToString("N")),
                "absolute" => Path.Combine(owners.Disk.Root, "UnrelatedUsers"),
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
            if (route == "relative") Assert.False(Path.IsPathFullyQualified(supplied!));
            if (route == "absolute") Assert.True(Path.IsPathFullyQualified(supplied!));
            var files = owners.Disk.Files();
            foreach (var bindProducer in new[] { false, true })
            {
                var published = new List<(WorldSession? Previous, WorldSession Next)>();
                var store = new WorldSessionStore((_, previous, next) => published.Add((previous, next)));
                if (bindProducer) store.BindMailboxPackages(owners.Producer);
                var connection = new ConquerTcpConnection(null, null);
                var first = owners.Session(supplied);
                var second = first with { Character = first.Character with { Money = first.Character.Money + 1 } };

                store.Set(connection, first);
                Assert.True(store.TryCompareAndSet(connection, first, second));

                AssertCurrent(store, connection, second);
                Assert.Equal(supplied, second.UsersRoot);
                Assert.Equal(2, published.Count);
                Assert.Null(published[0].Previous);
                Assert.Same(first, published[0].Next);
                Assert.Same(first, published[1].Previous);
                Assert.Same(second, published[1].Next);
                Assert.True(store.TryRemove(connection, out var removed));
                Assert.Same(second, removed);
                Assert.False(store.TryGet(connection, out _));
                Assert.Empty(store.Snapshot());
                Assert.Equal(2, published.Count);
            }
            owners.Disk.AssertFiles(files);
        });
}
