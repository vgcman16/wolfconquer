using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Nobility = WolfConquer.Core.World.LegacyNativeNobility;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityPersistenceTests
{
    [Fact]
    public Task MissingUsersDirectoryThrowsBeforeAnyRankingOrPacketMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(createUsers: false);
        var retained = new Nobility(77, "retained", 400000001, 1002, 1) { Position = 9 };
        scope.Ranking.ClientPoll.Add(77, retained);
        Assert.Throws<DirectoryNotFoundException>(() => LegacyNativeNobilityTable.Load());
        Assert.Same(retained, Assert.Single(scope.Ranking.ClientPoll).Value);
        Assert.Equal(9, retained.Position);
        Assert.Equal(12, scope.Available);
        Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task FullLoadReadsEveryExtensionAndPreservesNativeIniDefaultsMeshAndDifferentGenderRule() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Files.Seed("a.ini", Ini(101, "狼爵", 300000001, face: "7"));
        scope.Files.Seed("b.txt", Ini(102, "body-three", 300000002, body: "1003"));
        scope.Files.Seed("c", Ini(103, "body-eight", 300000003, body: "1008"));
        scope.Files.Seed("d.dat", "[Character]\r\nBody=65536\r\nFace=-1\r\nUID=invalid\r\nDonationNobility=400000001\r\n");
        scope.Files.Seed("e.tmp", "Not an INI section.\r\n");
        LegacyNativeNobilityTable.Load();
        Assert.Equal(4, scope.Ranking.ClientPoll.Count);
        var first = scope.Ranking.ClientPoll[101];
        Assert.Equal("狼爵", first.Name); Assert.Equal(71002u, first.Mesh); Assert.Equal((byte)1, first.Gender);
        var three = scope.Ranking.ClientPoll[102];
        Assert.Equal(1003u, three.Mesh); Assert.Equal((byte)0, three.Gender);
        scope.Player.Body = 1003;
        Assert.Equal((byte)1, scope.Player.GetGender); // Table's native rule is deliberately different.
        Assert.Equal((byte)0, scope.Ranking.ClientPoll[103].Gender);
        var defaults = scope.Ranking.ClientPoll[0];
        Assert.Equal("None", defaults.Name); Assert.Equal(1002u, defaults.Mesh);
        Assert.Equal((byte)1, defaults.Gender); Assert.Equal(400000001ul, defaults.Donation);
        Assert.Equal(7, scope.Available); // Even the non-INI .tmp row entered real UpdateRank.
        Assert.Empty(scope.Native.Queue);
        scope.Files.Unchanged();
    });

    [Fact]
    public Task DuplicateUidUsesActualGetFilesFirstObjectAndRepeatedLoadDoesNotReplaceIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        string z = scope.Files.Seed("z.txt", Ini(99, "z-first-if-enumerated", 300000001));
        string a = scope.Files.Seed("a.ini", Ini(99, "a-first-if-enumerated", 500000001));
        string[] order = Directory.GetFiles(scope.Files.Users); // Native order, no creation/name-order assumption.
        Assert.Equal(2, order.Length);
        Assert.Contains(order[0], new[] { z, a });
        LegacyNativeNobilityTable.Load();
        var row = Assert.Single(scope.Ranking.ClientPoll).Value;
        Assert.Equal(order[0] == z ? "z-first-if-enumerated" : "a-first-if-enumerated", row.Name);
        Assert.Equal(order[0] == z ? 300000001ul : 500000001ul, row.Donation);
        Assert.Equal(0, row.Position);
        LegacyNativeNobilityTable.Load();
        Assert.Same(row, Assert.Single(scope.Ranking.ClientPoll).Value);
        Assert.Equal(8, scope.Available);
        scope.Files.Unchanged();
    });

    [Fact]
    public Task RawLocationWithoutTrailingSeparatorAndDotDotUsesLiteralUsersSuffix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Files.Seed("main.ini", Ini(11, "root-users", 300000001));
        string prefix = scope.Files.Prefix();
        scope.Files.Seed("other.ini", Ini(22, "prefix-users", 400000001), prefix);
        LegacyNativeServerConfig.DbLocation = prefix + "\\..";
        LegacyNativeNobilityTable.Load();
        Assert.Equal(11u, Assert.Single(scope.Ranking.ClientPoll).Key);
        Assert.Equal("root-users", scope.Ranking.ClientPoll[11].Name);
        Assert.Equal(11, scope.Available);
        scope.Files.Unchanged();
    });

    [Fact]
    public Task LaterUnpublishedGamePollFaultKeepsSuccessfulLoadPrefixAndPartiallyPublishedRank() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Files.Seed("first.ini", "placeholder");
        scope.Files.Seed("second.ini", "placeholder");
        string[] order = Directory.GetFiles(scope.Files.Users);
        Assert.Equal(2, order.Length);
        scope.Files.RewriteBeforeRead(order[0], Ini(11, "filtered-prefix", 1));
        scope.Files.RewriteBeforeRead(order[1], Ini(22, "faulting-second-row", 300000001));
        LegacyNativePoolClients.GamePoll = null;
        Assert.Throws<NullReferenceException>(() => LegacyNativeNobilityTable.Load());
        var row = Assert.Single(scope.Ranking.ClientPoll);
        Assert.Equal(22u, row.Key); Assert.Equal("faulting-second-row", row.Value.Name); Assert.Equal(0, row.Value.Position);
        Assert.Equal(10, scope.Available); // Complete first rank call, then fault in the second.
        scope.UnserializedDisposed(0); scope.UnserializedDisposed(1);
        Assert.Empty(scope.Native.Queue);
        scope.Files.Unchanged();
    });
}
