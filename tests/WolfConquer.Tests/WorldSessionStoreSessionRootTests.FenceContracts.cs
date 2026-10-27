using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    [Theory]
    [MemberData(nameof(InvalidProvidedRoots))]
    public void InvalidPreviousOrNextRootIsRejectedByBothExistingMutationFenceContracts(string invalidRoot) =>
        WithRegisteredOwners(owners =>
        {
            var files = owners.Disk.Files();
            var current = owners.Session();
            var invalid = current with { UsersRoot = invalidRoot };

            // Store preflight is tested separately. These existing bool APIs
            // must also reject invalid state without throwing or treating it as absence.
            Assert.False(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(null, invalid, null));
            Assert.False(LegacyOrdinaryAdmissionMutationFence.CanMutateSession(invalid, current, null));
            Assert.False(LegacyMailboxPackageMutationFence.CanMutateSession(null, null, invalid));
            Assert.False(LegacyMailboxPackageMutationFence.CanMutateSession(null, invalid, current));
            Assert.False(LegacyMailboxPackageMutationFence.CanMutateSession(owners.Producer, null, invalid));
            Assert.False(LegacyMailboxPackageMutationFence.CanMutateSession(owners.Producer, invalid, current));

            owners.Disk.AssertFiles(files);
        });

    [Fact]
    public void WindowsUncVolumeLabelsRemainValidWhileReservedFilesystemComponentsReject()
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
            var invalid = current with { UsersRoot = @"\\server\share\NUL\Users" };

            AssertInvalidRoot(() => store.TryCompareAndSet(connection, current, invalid));
            AssertCurrent(store, connection, current);
            Assert.Equal(0, changes);

            // Server/share labels are lexical volume metadata. These roots are
            // unrelated to every real owner and cause no network or file lookup.
            var valid = current with { UsersRoot = @"\\NUL\CON\Users" };
            Assert.True(store.TryCompareAndSet(connection, current, valid));
            AssertCurrent(store, connection, valid);
            Assert.Equal(1, changes);
            owners.Disk.AssertFiles(files);
        });
    }
}
