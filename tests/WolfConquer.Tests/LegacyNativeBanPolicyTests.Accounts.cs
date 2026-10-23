using WolfConquer.Core.World;
using AccountBan = WolfConquer.Core.Data.LegacyNativeSystemBannedAccount;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBanPolicyTests
{
    [Fact]
    public void AddByUidSetsEveryFieldButDuplicateDoesNotRefreshAnyOfThem()
    {
        using var state = new State();
        long before = DateTime.Now.Ticks;
        AccountBan.AddBan(17, "Wolf", 7, "First");
        long after = DateTime.Now.Ticks;
        var row = Assert.Single(AccountBan.BannedPoll).Value;
        Assert.Equal(17u, row.UID);
        Assert.Equal("Wolf", row.Name);
        Assert.Equal(7u, row.Hours);
        Assert.Equal("First", row.Reason);
        Assert.InRange(row.StartBan, before, after);
        AccountBan.BannedPoll.GetValues();
        AccountBan.AddBan(17, "Changed", uint.MaxValue, "Changed");
        Assert.Same(row, AccountBan.BannedPoll[17]);
        Assert.Equal("Wolf", row.Name);
        Assert.Equal(7u, row.Hours);
        Assert.Equal("First", row.Reason);
        Assert.False(AccountBan.BannedPoll.Update);
    }

    [Fact]
    public void AddByClientPublishesActualPlayerIdentityThenIncrementsOnlyPlayerBanCountWithNativeWrap()
    {
        using var state = new State();
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 19;
        client.Player.Name = "LiveName";
        client.Player.BanCount = uint.MaxValue;
        client.BanCount = 12;
        long before = DateTime.Now.Ticks;
        AccountBan.AddBan(client, 5);
        long after = DateTime.Now.Ticks;
        var row = Assert.Single(AccountBan.BannedPoll).Value;
        Assert.Equal(19u, row.UID);
        Assert.Equal("LiveName", row.Name);
        Assert.Equal(5u, row.Hours);
        Assert.Equal("", row.Reason);
        Assert.InRange(row.StartBan, before, after);
        Assert.Equal(0u, client.Player.BanCount);
        Assert.Equal((byte)12, client.BanCount);
        client.Player.Name = "Later";
        AccountBan.AddBan(client, 9, "Later");
        Assert.Same(row, AccountBan.BannedPoll[19]);
        Assert.Equal("LiveName", row.Name);
        Assert.Equal(0u, client.Player.BanCount);
    }

    [Fact]
    public void NullAccountNameAndReasonAreRetainedAndLiveMessageUsesEmptyConcatenation()
    {
        using var state = new State();
        AccountBan.AddBan(17, null, 7, null);
        var row = AccountBan.BannedPoll[17];
        Assert.Null(row.Name);
        Assert.Null(row.Reason);
        DateTime before = DateTime.Now;
        Assert.True(AccountBan.IsBanned(17, out string message));
        DateTime after = DateTime.Now;
        AssertRemaining(message, DateTime.FromBinary(row.StartBan).AddHours(7), before, after, true, null);
        Assert.Throws<NullReferenceException>(() => row.ToString());
    }

    [Fact]
    public void RemoveByUidDistinguishesAnAbsentKeyFromARealMutation()
    {
        using var state = new State();
        AccountBan.BannedPoll.Add(17, Account(17));
        AccountBan.BannedPoll.GetValues();
        AccountBan.RemoveBan(404u);
        Assert.False(AccountBan.BannedPoll.Update);
        AccountBan.RemoveBan(17u);
        Assert.Empty(AccountBan.BannedPoll);
        Assert.True(AccountBan.BannedPoll.Update);
    }

    [Theory]
    [InlineData("Wolf")]
    [InlineData(null)]
    public void RemoveByNameUsesTheMatchingRecordUid(string? name)
    {
        using var state = new State();
        var alias = Account(20, name);
        AccountBan.BannedPoll.Add(10, alias);
        AccountBan.BannedPoll.Add(20, Account(20, "Other"));
        AccountBan.RemoveBan(name);
        Assert.Same(alias, Assert.Single(AccountBan.BannedPoll).Value);
        Assert.True(AccountBan.BannedPoll.ContainsKey(10));
    }

    [Fact]
    public void NameRemovalRetainsUidZeroSentinelAndMissingNameDoesNotDirtyTheDictionary()
    {
        using var state = new State();
        var row = Account(0, "Zero");
        AccountBan.BannedPoll.Add(0, row);
        AccountBan.BannedPoll.GetValues();
        AccountBan.RemoveBan("missing");
        Assert.False(AccountBan.BannedPoll.Update);
        AccountBan.RemoveBan("Zero");
        Assert.Same(row, Assert.Single(AccountBan.BannedPoll).Value);
        Assert.False(AccountBan.BannedPoll.Update);
    }

    [Fact]
    public void MissingAndExpiredAccountsReturnEmptyMessagesAndExpireByRecordUid()
    {
        using var state = new State();
        Assert.False(AccountBan.IsBanned(404, out string missing));
        Assert.Equal("", missing);
        var alias = Account(20);
        AccountBan.BannedPoll.Add(10, alias);
        AccountBan.BannedPoll.Add(20, Account(20, "Other", 1, DateTime.Now.Ticks));
        Assert.False(AccountBan.IsBanned(10, out string expired));
        Assert.Equal("", expired);
        Assert.Same(alias, Assert.Single(AccountBan.BannedPoll).Value);
        Assert.True(AccountBan.BannedPoll.ContainsKey(10));
        Assert.False(AccountBan.BannedPoll.ContainsKey(20));
    }

    [Fact]
    public void ActiveAccountReportsOriginalReasonWithoutRemovingItsRecord()
    {
        using var state = new State();
        var row = Account(17, "Wolf", 5, DateTime.Now.AddDays(2).AddMinutes(70).Ticks, "Original / reason");
        AccountBan.BannedPoll.Add(17, row);
        AccountBan.BannedPoll.GetValues();
        DateTime before = DateTime.Now;
        Assert.True(AccountBan.IsBanned(17, out string message));
        DateTime after = DateTime.Now;
        AssertRemaining(message, DateTime.FromBinary(row.StartBan).AddHours(row.Hours), before, after, true, row.Reason);
        Assert.Same(row, AccountBan.BannedPoll[17]);
        Assert.False(AccountBan.BannedPoll.Update);
    }
}
