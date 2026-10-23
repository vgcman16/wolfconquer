using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Theory]
    [InlineData("A B")]
    [InlineData("A@B")]
    [InlineData("A\tB")]
    [InlineData("A\0B")]
    [InlineData("A\nB")]
    [InlineData("A🐺B")]
    [InlineData("AéB")]
    public void PublishedClanAdmissionRejectsInvalidInteriorNamesWithoutChangingRealState(string name)
    {
        using var state = new State();
        var clans = LegacyNativeClanRuntime.Clans;
        var queue = LegacyNativeClanRuntime.ChangeNameRecords;
        var counter = LegacyNativeClanRuntime.CounterClansID;
        var existing = Clan(1, "Existing");
        clans[1] = existing;
        queue[77] = "Queued";
        uint before = counter.Count;

        Assert.False(LegacyNativeClanRuntime.AllowCreateClan(name));

        Assert.Same(clans, LegacyNativeClanRuntime.Clans);
        Assert.Same(queue, LegacyNativeClanRuntime.ChangeNameRecords);
        Assert.Same(counter, LegacyNativeClanRuntime.CounterClansID);
        Assert.Equal(before, counter.Count);
        Assert.Single(clans); Assert.Same(existing, clans[1]); Assert.Equal("Existing", existing.Name);
        Assert.Single(queue); Assert.Equal("Queued", queue[77]);
    }

    [Fact]
    public void PublishedClanQueryRetainsValidNamesAndExistingExactReservationsWithoutCreatingAClan()
    {
        using var state = new State();
        var existing = Clan(1, "Existing");
        LegacyNativeClanRuntime.Clans[1] = existing;
        LegacyNativeClanRuntime.ChangeNameRecords[77] = "Queued";
        uint before = LegacyNativeClanRuntime.CounterClansID.Count;

        Assert.True(LegacyNativeClanRuntime.AllowCreateClan("狼*~.&$123"));
        Assert.True(LegacyNativeClanRuntime.AllowCreateClan("WolfPack"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("Existing"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("Queued"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("A B"));
        Assert.False(LegacyNativeClanRuntime.AllowCreateClan("A@B"));
        Assert.Equal(before, LegacyNativeClanRuntime.CounterClansID.Count);
        Assert.Single(LegacyNativeClanRuntime.Clans);
        Assert.Same(existing, LegacyNativeClanRuntime.Clans[1]);
        Assert.Single(LegacyNativeClanRuntime.ChangeNameRecords);
    }
}
