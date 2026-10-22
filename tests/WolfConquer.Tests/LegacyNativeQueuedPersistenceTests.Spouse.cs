using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Leaves = WolfConquer.Core.Data.LegacyNativeServerDatabasePersistence;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQueuedPersistenceTests
{
    [WindowsNativeIniFact]
    public void UpdateSpouseZeroUidDoesNotTouchOwnFileOrPhysicalSpouseName()
    {
        using var files = new Files();
        string own = files.User(17);
        files.Seed(own);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 17;
        client.Player.Spouse = "Physical spouse";
        client.Player.Name = "Physical name";
        Leaves.UpdateSpouse(client);
        Assert.Equal(UserRows, File.ReadAllText(own));
        Assert.Equal("Physical spouse", client.Player.Spouse);
        Assert.Equal(0u, client.Player.SpouseUID);
        Assert.Single(Directory.GetFiles(files.Users));
    }

    [WindowsNativeIniFact]
    public void UpdateSpouseOnlyWritesPhysicalPlayerNameToSpouseUidProfile()
    {
        using var files = new Files();
        string own = files.User(17);
        string spouse = files.User(29);
        files.Seed(own);
        files.Seed(spouse);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 17;
        client.Player.SpouseUID = 29;
        client.Player.Spouse = "Not modified";
        client.Player.Name = "狼 renamed";

        Leaves.UpdateSpouse(client);

        Assert.Equal(UserRows, File.ReadAllText(own));
        Assert.Equal(UserRows.Replace("Spouse=StoredSpouse\r\n", "Spouse=狼 renamed\r\n", StringComparison.Ordinal),
            File.ReadAllText(spouse));
        Assert.Equal("狼 renamed", files.Profile(spouse).ReadString("Character", "Spouse", "missing"));
        Assert.Equal(88u, files.Profile(spouse).ReadUInt32("Character", "SpouseUID"));
        Assert.Equal(29u, client.Player.SpouseUID);
        Assert.Equal("Not modified", client.Player.Spouse);
    }

    [WindowsNativeIniFact]
    public void UpdateSpouseNullNameRetainsNativeWriteStringLiteralZero()
    {
        using var files = new Files();
        string spouse = files.User(29);
        files.Seed(spouse);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.SpouseUID = 29;
        client.Player.Name = null!;
        Leaves.UpdateSpouse(client);
        Assert.Equal(UserRows.Replace("Spouse=StoredSpouse\r\n", "Spouse=0\r\n", StringComparison.Ordinal),
            File.ReadAllText(spouse));
        Assert.Null(client.Player.Name);
        Assert.Equal(29u, client.Player.SpouseUID);
    }

    [WindowsNativeIniFact]
    public void UpdateSpouseMissingParentIgnoresNativeWriteFailureWithoutMutatingPhysicalIdentity()
    {
        using var files = new Files(users: false);
        string spouse = files.User(29);
        var client = new LegacyNativeGameClientRuntime();
        client.Player.SpouseUID = 29;
        client.Player.Spouse = "Retained";
        client.Player.Name = "Current";
        Leaves.UpdateSpouse(client);
        Assert.False(File.Exists(spouse));
        Assert.False(Directory.Exists(files.Users));
        Assert.Equal(29u, client.Player.SpouseUID);
        Assert.Equal("Retained", client.Player.Spouse);
        Assert.Equal("Current", client.Player.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullSpouseClientRetainsNativeFaultBeforeAnyIniUse(bool destroy)
    {
        using var files = new Files();
        if (destroy) Assert.Throws<NullReferenceException>(() => Leaves.DestroySpouse(null!));
        else Assert.Throws<NullReferenceException>(() => Leaves.UpdateSpouse(null!));
        Assert.Empty(Directory.GetFiles(files.Users));
    }

    [WindowsNativeIniFact]
    public Task DestroySpouseZeroUidStillChangesNameAndQueuesExactOwnPlayerSpouseString() =>
        NativePacketTestThread.Run(() =>
        {
            using var files = new Files();
            using var packets = new SpousePackets();
            string own = files.User(17);
            files.Seed(own);
            packets.Client.Player.Spouse = "Prior";

            Leaves.DestroySpouse(packets.Client);

            Assert.Equal("None", packets.Client.Player.Spouse);
            Assert.Equal(0u, packets.Client.Player.SpouseUID);
            Assert.Equal(SpouseNoneFrame, Assert.Single(packets.Queue));
            Assert.Equal(SpouseNoneFrame, NativeSpellTestFixture.Bytes(packets.Native.Packet));
            Assert.Equal(0, packets.Native.Packet.Position); // Actual RecycledPacket disposal calls Reuse.
            Assert.Equal(0, LegacyNativePacketRecycle.Count);
            Assert.Equal(UserRows, File.ReadAllText(own));
            Assert.Single(Directory.GetFiles(files.Users));
        });

    [WindowsNativeIniFact]
    public Task DestroySpouseWritesOnlyRemoteRowsThenClearsLocalPhysicalIdentityAndNotifiesOwnUid() =>
        NativePacketTestThread.Run(() =>
        {
            using var files = new Files();
            using var packets = new SpousePackets();
            string own = files.User(17);
            string spouse = files.User(29);
            files.Seed(own);
            files.Seed(spouse);
            packets.Client.Player.SpouseUID = 29;
            packets.Client.Player.Spouse = "Prior";
            packets.Client.Player.Name = "Retained name";

            Leaves.DestroySpouse(packets.Client);

            Assert.Equal(UserRows.Replace("SpouseUID=88\r\n", "SpouseUID=0\r\n", StringComparison.Ordinal)
                .Replace("Spouse=StoredSpouse\r\n", "Spouse=None\r\n", StringComparison.Ordinal),
                File.ReadAllText(spouse));
            Assert.Equal(UserRows, File.ReadAllText(own));
            Assert.Equal(0u, packets.Client.Player.SpouseUID);
            Assert.Equal("None", packets.Client.Player.Spouse);
            Assert.Equal("Retained name", packets.Client.Player.Name);
            Assert.Equal(17u, packets.Client.Player.UID);
            Assert.Equal(SpouseNoneFrame, Assert.Single(packets.Queue));
        });

    [WindowsNativeIniFact]
    public Task DestroySpouseMissingParentStillClearsPhysicalIdentityAndQueuesNotification() =>
        NativePacketTestThread.Run(() =>
        {
            using var files = new Files(users: false);
            using var packets = new SpousePackets();
            string spouse = files.User(29);
            packets.Client.Player.SpouseUID = 29;
            packets.Client.Player.Spouse = "Prior";

            Leaves.DestroySpouse(packets.Client);

            Assert.False(File.Exists(spouse));
            Assert.False(Directory.Exists(files.Users));
            Assert.Equal(0u, packets.Client.Player.SpouseUID);
            Assert.Equal("None", packets.Client.Player.Spouse);
            Assert.Equal(SpouseNoneFrame, Assert.Single(packets.Queue));
        });

    [WindowsNativeIniFact]
    public Task DestroySpouseFakeClientSuppressesQueueOnlyAfterFileAndPhysicalMutations() =>
        NativePacketTestThread.Run(() =>
        {
            using var files = new Files();
            using var packets = new SpousePackets();
            string spouse = files.User(29);
            files.Seed(spouse);
            packets.Client.Player.SpouseUID = 29;
            packets.Client.Player.Spouse = "Prior";
            packets.Client.Fake = true;

            Leaves.DestroySpouse(packets.Client);

            Assert.Equal(UserRows.Replace("SpouseUID=88\r\n", "SpouseUID=0\r\n", StringComparison.Ordinal)
                .Replace("Spouse=StoredSpouse\r\n", "Spouse=None\r\n", StringComparison.Ordinal),
                File.ReadAllText(spouse));
            Assert.Equal(0u, packets.Client.Player.SpouseUID);
            Assert.Equal("None", packets.Client.Player.Spouse);
            Assert.Equal(SpouseNoneFrame, NativeSpellTestFixture.Bytes(packets.Native.Packet));
            Assert.Empty(packets.Queue);
            Assert.Equal(0, packets.Native.Packet.Position);
        });
}
