using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    [Fact]
    public void RealPublishedPendingJournalStillFencesItsMatchingSessionBeforePersistenceOrPublication() =>
        WithRegisteredOwners(owners =>
        {
            var changes = 0;
            var persistenceCalls = 0;
            var store = new WorldSessionStore((_, _, _) => changes++);
            var connection = new ConquerTcpConnection(null, null);
            var current = owners.Session(owners.Disk.Users);
            store.Set(connection, current);
            changes = 0;
            owners.Disk.StagePendingSnapshotJournal();
            var files = owners.Disk.Files();
            var updated = current with { Character = current.Character with { Money = current.Character.Money + 1 } };
            bool UnexpectedPersistence()
            {
                persistenceCalls++;
                throw new InvalidOperationException("Pending journal reached ordinary persistence.");
            }

            Assert.Throws<InvalidOperationException>(() => store.Set(connection, updated));
            Assert.False(store.TryCompareAndSet(connection, current, updated));
            Assert.False(store.TryCommitSession(connection, current, updated, UnexpectedPersistence));

            AssertCurrent(store, connection, current);
            Assert.Equal(0, changes);
            Assert.Equal(0, persistenceCalls);
            Assert.True(LegacyTradeJournal.HasPending(owners.Disk.Users));
            owners.Disk.AssertFiles(files);
        });

    [Fact]
    public void ClosedGenuineOwnersRemainFencesForTheirRootWithoutBlockingExplicitStorageFreeSessions() =>
        WithRegisteredOwners(owners =>
        {
            var changes = 0;
            var store = new WorldSessionStore((_, _, _) => changes++);
            var connection = new ConquerTcpConnection(null, null);
            var current = owners.Session(owners.Disk.Users);
            store.Set(connection, current);
            changes = 0;
            owners.Runtime.Dispose();
            Assert.False(owners.Runtime.Ordinary.IsCurrent(owners.Runtime.Ordinary.Roots));
            Assert.False(owners.Producer.TryRequireAccountWritable(current.AccountId, out _));
            var files = owners.Disk.Files();
            var updated = current with { Character = current.Character with { Money = current.Character.Money + 1 } };

            Assert.Throws<InvalidOperationException>(() => store.Set(connection, updated));
            Assert.False(store.TryCompareAndSet(connection, current, updated));
            AssertCurrent(store, connection, current);
            Assert.Equal(0, changes);

            var storageFreeConnection = new ConquerTcpConnection(null, null);
            var storageFree = current with { UsersRoot = null };
            store.Set(storageFreeConnection, storageFree);
            AssertCurrent(store, storageFreeConnection, storageFree);
            Assert.Equal(1, changes);
            owners.Disk.AssertFiles(files);
        });
}
