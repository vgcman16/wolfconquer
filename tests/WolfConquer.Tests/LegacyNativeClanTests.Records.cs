using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClanTests
{
    [Fact]
    public void ImplicitBirthRetainsAllDeclaredInitializersWithoutAllocatingAnId()
    {
        using var state = new State();
        var a = new LegacyNativeClanRuntime();
        var b = new LegacyNativeClanRuntime();
        Assert.Equal(10u, LegacyNativeClanRuntime.CounterClansID.Count);
        Assert.Equal(0u, a.ID);
        Assert.Equal((byte)1, a.Level);
        Assert.Equal("None", a.ClanBuletin);
        Assert.Equal("", a.Name);
        Assert.Equal("", a.LeaderName);
        Assert.Equal(0u, a.RequestAlly);
        Assert.Equal(0u, a.Donation);
        Assert.Equal((byte)0, a.BP);
        Assert.Empty(a.Ally);
        Assert.Empty(a.Enemy);
        Assert.Empty(a.Members);
        Assert.NotSame(a.Ally, b.Ally);
        Assert.NotSame(a.Members, b.Members);
        Assert.Equal(new uint[] { 1000000, 2000000, 3500000, 7500000 }, LegacyNativeClanRuntime.UpdateLevelAmount);
        Assert.Equal(new uint[] { 2500000, 12000000, 35000000, 120000000 }, LegacyNativeClanRuntime.UpdateBPAmount);
    }

    [Fact]
    public void WholeRecordRoundTripsExactNativeOrderAndTrailingZeroFields()
    {
        var clan = new LegacyNativeClanRuntime { ID = 17, Name = "Wolf", LeaderName = "Chief", Level = 4,
            Donation = uint.MaxValue, ClanBuletin = "Welcome", BP = 3 };
        Assert.Equal("17/Wolf/Chief/4/4294967295/Welcome/3/0/0/0/", clan.ToString());
        var loaded = new LegacyNativeClanRuntime();
        loaded.Load(clan.ToString());
        Assert.Equal(clan.ToString(), loaded.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullAndEmptyLoadLeaveExistingStateUnchanged(string? line)
    {
        var clan = Clan(8, "Before");
        clan.Load(line);
        Assert.Equal(8u, clan.ID);
        Assert.Equal("Before", clan.Name);
        Assert.Equal((byte)1, clan.Level);
    }

    [Fact]
    public void SlashlessNonemptyLoadResetsToReadDefaultsButDoesNotResetRelations()
    {
        var clan = Clan(8, "Before");
        var other = Clan(9, "Ally");
        clan.Ally[9] = other;
        clan.Load("invalid");
        Assert.Equal("0/None/None/0/0/None/0/0/0/0/", clan.ToString());
        Assert.Same(other, clan.Ally[9]);
    }

    [Fact]
    public void EmptyStringTokensAreNotReplacedWithDefaultsAndOverflowNumbersAre()
    {
        var clan = Clan(8, "Before");
        clan.Load("bad///256/4294967296//999/");
        Assert.Equal(0u, clan.ID);
        Assert.Equal("", clan.Name);
        Assert.Equal("", clan.LeaderName);
        Assert.Equal((byte)0, clan.Level);
        Assert.Equal(0u, clan.Donation);
        Assert.Equal("", clan.ClanBuletin);
        Assert.Equal((byte)0, clan.BP);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void NullRecordStringStillFaultsAtItsOriginalWritePosition(int slot)
    {
        var clan = Clan(8, "Name");
        if (slot == 0) clan.Name = null;
        if (slot == 1) clan.LeaderName = null;
        if (slot == 2) clan.ClanBuletin = null;
        Assert.Throws<NullReferenceException>(() => clan.ToString());
    }

    [Fact]
    public void RelationRecordsSerializeValueIdsRatherThanKeysAndRetainDuplicateAliases()
    {
        var clan = Clan(1, "One");
        var other = Clan(7, "Other");
        clan.Ally[100] = other;
        clan.Ally[200] = other;
        clan.Enemy[300] = Clan(9, "Enemy");
        Assert.Equal("2/7/7/", clan.SaveAlly());
        Assert.Equal("1/9/", clan.SaveEnemy());
        Assert.Equal("0/", new LegacyNativeClanRuntime().SaveAlly());
    }

    [Fact]
    public void LazyMemberCacheUsesUidAndKeepsCachedIdentityUntilExplicitlyCleared()
    {
        using var state = new State();
        var owner = NativeSpellTestFixture.CreateOwner(new());
        owner.Player.UID = 17;
        var a = Clan(1, "First");
        var b = Clan(2, "Second");
        var first = new LegacyNativeClanMember { UID = 17, Name = "FirstMember" };
        var second = new LegacyNativeClanMember { UID = 17, Name = "SecondMember" };
        a.Members[17] = first;
        b.Members[17] = second;
        owner.Player.MyClan = a;
        Assert.Same(first, owner.Player.MyClanMember);
        owner.Player.MyClan = b;
        Assert.Same(first, owner.Player.MyClanMember);
        owner.Player.MyClanMember = null;
        Assert.Same(second, owner.Player.MyClanMember);
        b.Members.Clear();
        owner.Player.MyClanMember = null;
        Assert.Null(owner.Player.MyClanMember);
        owner.Player.MyClan = null;
        Assert.Null(owner.Player.MyClanMember);
    }
}
