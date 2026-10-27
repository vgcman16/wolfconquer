using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldSessionStoreSessionRootTests
{
    [Theory]
    [InlineData("trailing-space")]
    [InlineData("space-before-parent")]
    [InlineData("reserved-before-parent")]
    [InlineData("alternate-device")]
    public void SuppliedWindowsAliasComponentsRejectBeforeNormalizationCanEraseThem(string fault)
    {
        if (!OperatingSystem.IsWindows()) return;
        WithRegisteredOwners(owners =>
        {
            var root = fault switch
            {
                "trailing-space" => Path.Combine(owners.Disk.Root, "UnrelatedUsers "),
                "space-before-parent" => Path.Combine(owners.Disk.Root, "Alias ", "..", "UnrelatedUsers"),
                "reserved-before-parent" => Path.Combine(owners.Disk.Root, "NUL", "..", "UnrelatedUsers"),
                "alternate-device" => "//./C:/Users",
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            var changes = 0;
            var store = new WorldSessionStore((_, _, _) => changes++);
            store.BindMailboxPackages(owners.Producer);
            var connection = new ConquerTcpConnection(null, null);
            var current = owners.Session();
            store.Set(connection, current);
            changes = 0;
            var files = owners.Disk.Files();
            var invalid = current with { UsersRoot = root };

            AssertInvalidRoot(() => store.Set(connection, invalid));
            AssertInvalidRoot(() => store.TryCompareAndSet(connection, current, invalid));

            AssertCurrent(store, connection, current);
            Assert.Equal(0, changes);
            owners.Disk.AssertFiles(files);
        });
    }

    [Fact]
    public void WindowsNavigationRetainsRelativeAbsoluteDriveRelativeAndUncStorePublication()
    {
        if (!OperatingSystem.IsWindows()) return;
        WithRegisteredOwners(owners =>
        {
            var marker = "wolf-root-navigation-" + Guid.NewGuid().ToString("N");
            var roots = new List<(string Kind, string Root)>
            {
                ("relative", Path.Combine(".", "via", "..", marker)),
                ("absolute", Path.Combine(owners.Disk.Root, "via", "..", marker)),
                ("UNC", @"\\NUL\CON\via\..\Users"),
                ("alternate-separator UNC", "//NUL/CON/./via/../Users"),
            };
            var volume = Path.GetPathRoot(Environment.CurrentDirectory);
            if (volume is { Length: >= 2 } && volume[1] == ':')
            {
                var driveRelative = volume[..2] + @".\via\..\" + marker;
                Assert.False(Path.IsPathFullyQualified(driveRelative));
                roots.Add(("drive-relative", driveRelative));
            }
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
                foreach (var supplied in roots)
                {
                    var next = current with { UsersRoot = supplied.Root };
                    Assert.True(store.TryCompareAndSet(connection, current, next),
                        $"Valid {supplied.Kind} navigation was rejected; boundProducer={bindProducer}.");
                    AssertCurrent(store, connection, next);
                    Assert.Equal(supplied.Root, next.UsersRoot);
                    current = next;
                }
                Assert.Equal(roots.Count, changes);
            }
            owners.Disk.AssertFiles(files);
        });
    }
}
