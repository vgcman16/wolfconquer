using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    [Theory]
    [MemberData(nameof(InvalidProvidedRoots))]
    public void InvalidProvidedRootCannotReachPersistenceOrPartiallyPublishEitherTransactionParticipant(string invalidRoot) =>
        WithRegisteredOwners(owners =>
        {
            var files = owners.Disk.Files();
            foreach (var bindProducer in new[] { false, true })
            {
                var changes = 0;
                var persistenceCalls = 0;
                var store = new WorldSessionStore((_, _, _) => changes++);
                if (bindProducer) store.BindMailboxPackages(owners.Producer);
                var firstConnection = new ConquerTcpConnection(null, null);
                var secondConnection = new ConquerTcpConnection(null, null);
                var first = owners.Session();
                var second = first with
                {
                    AccountId = first.AccountId + 1,
                    Character = first.Character with { Uid = first.Character.Uid + 1 },
                    PlayerItems = WolfConquer.Core.Data.PlayerItemSnapshot.Empty,
                };
                store.Set(firstConnection, first);
                store.Set(secondConnection, second);
                changes = 0;
                var updatedFirst = first with { Character = first.Character with { Money = first.Character.Money + 1 } };
                var invalid = second with { UsersRoot = invalidRoot };
                bool UnexpectedPersistence()
                {
                    persistenceCalls++;
                    throw new InvalidOperationException("Invalid-root transaction reached persistence.");
                }
                WorldSession? UnexpectedPreparation()
                {
                    persistenceCalls++;
                    throw new InvalidOperationException("Invalid-root transaction reached prepare-and-persist.");
                }

                AssertInvalidRoot(() => store.TryCommitSession(secondConnection, second, invalid, UnexpectedPersistence));
                AssertInvalidRoot(() => store.TryCommitSession(secondConnection, invalid, second, UnexpectedPersistence));
                AssertInvalidRoot(() => store.TryCommitPair(firstConnection, first, updatedFirst,
                    secondConnection, second, invalid, UnexpectedPersistence));
                if (bindProducer)
                    AssertInvalidRoot(() => store.TryCommitPreparedSession(secondConnection, invalid,
                        owners.Producer, UnexpectedPreparation));

                AssertCurrent(store, firstConnection, first);
                AssertCurrent(store, secondConnection, second);
                Assert.Equal(2, store.Snapshot().Count);
                Assert.Equal(0, persistenceCalls);
                Assert.Equal(0, changes);
            }
            owners.Disk.AssertFiles(files);
        });

    [Fact]
    public void WindowsInvalidFilesystemComponentsCannotBecomeStorageFreeOrUnrelatedSessionRoutes()
    {
        if (!OperatingSystem.IsWindows()) return;
        WithRegisteredOwners(owners =>
        {
            var changes = 0;
            var store = new WorldSessionStore((_, _, _) => changes++);
            var connection = new ConquerTcpConnection(null, null);
            var current = owners.Session();
            store.Set(connection, current);
            changes = 0;
            var files = owners.Disk.Files();
            var invalid = new[]
            {
                Path.Combine(owners.Disk.Root, "wild*card"),
                Path.Combine(owners.Disk.Root, "Users:stream"),
                Path.Combine(owners.Disk.Root, "NUL.ini"),
                Path.Combine(owners.Disk.Root, "COM\u00b9.txt"),
                Path.Combine(owners.Disk.Root, "trailing."),
                @"\\.\C:\Users",
            };
            foreach (var root in invalid)
                AssertInvalidRoot(() => store.TryCompareAndSet(connection, current, current with { UsersRoot = root }));
            AssertCurrent(store, connection, current);
            Assert.Equal(0, changes);
            owners.Disk.AssertFiles(files);
        });
    }
}
