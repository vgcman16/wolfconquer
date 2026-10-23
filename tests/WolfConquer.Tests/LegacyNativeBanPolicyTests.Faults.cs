using WolfConquer.Core.World;
using IpBan = WolfConquer.Core.Data.LegacyNativeSystemBanned;
using AccountBan = WolfConquer.Core.Data.LegacyNativeSystemBannedAccount;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBanPolicyTests
{
    [Fact]
    public void NullIpArgumentsKeepTheUnderlyingDictionaryFaults()
    {
        using var state = new State();
        Assert.Throws<ArgumentNullException>(() => IpBan.AddBan(null, 1));
        Assert.Throws<ArgumentNullException>(() => IpBan.IsBanned(null!, out _));
        Assert.Empty(IpBan.BannedPoll);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullPublishedRecordFaultsWithoutExpiryRepair(bool account)
    {
        using var state = new State();
        if (account)
        {
            AccountBan.BannedPoll.Add(17, null!);
            Assert.Throws<NullReferenceException>(() => AccountBan.IsBanned(17, out _));
            Assert.True(AccountBan.BannedPoll.ContainsKey(17));
        }
        else
        {
            IpBan.BannedPoll.Add("ip", null!);
            Assert.Throws<NullReferenceException>(() => IpBan.IsBanned("ip", out _));
            Assert.True(IpBan.BannedPoll.ContainsKey("ip"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidBinaryTicksPropagateBeforeRecordRemoval(bool account)
    {
        using var state = new State();
        if (account)
        {
            AccountBan.BannedPoll.Add(17, Account(17, start: long.MaxValue));
            Assert.Throws<ArgumentException>(() => AccountBan.IsBanned(17, out _));
            Assert.Single(AccountBan.BannedPoll);
        }
        else
        {
            IpBan.BannedPoll.Add("ip", Ip("ip", start: long.MaxValue));
            Assert.Throws<ArgumentException>(() => IpBan.IsBanned("ip", out _));
            Assert.Single(IpBan.BannedPoll);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddHoursOverflowPropagatesBeforeExpiryRemoval(bool account)
    {
        using var state = new State();
        if (account)
        {
            AccountBan.BannedPoll.Add(17, Account(17, hours: uint.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => AccountBan.IsBanned(17, out _));
            Assert.Single(AccountBan.BannedPoll);
        }
        else
        {
            IpBan.BannedPoll.Add("ip", Ip("ip", hours: uint.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => IpBan.IsBanned("ip", out _));
            Assert.Single(IpBan.BannedPoll);
        }
    }

    [Fact]
    public void ExpiredRecordWithNullStoredIpFaultsAtRemovalAndKeepsItsLookupEntry()
    {
        using var state = new State();
        var row = new IpBan.Client { IP = null };
        IpBan.BannedPoll.Add("lookup", row);
        Assert.Throws<ArgumentNullException>(() => IpBan.IsBanned("lookup", out _));
        Assert.Same(row, IpBan.BannedPoll["lookup"]);
    }

    [Fact]
    public void NameAndIpRemovalDoNotSkipANullValue()
    {
        using var state = new State();
        AccountBan.BannedPoll.Add(17, null!);
        IpBan.BannedPoll.Add("ip", null!);
        Assert.Throws<NullReferenceException>(() => AccountBan.RemoveBan("Wolf"));
        Assert.Throws<NullReferenceException>(() => IpBan.RemoveBan("ip"));
        Assert.Single(AccountBan.BannedPoll);
        Assert.Single(IpBan.BannedPoll);
    }

    [Fact]
    public void ClientBanDoesNotInventMissingClientOrPlayerOwners()
    {
        using var state = new State();
        Assert.Throws<NullReferenceException>(() => AccountBan.AddBan((LegacyNativeGameClientRuntime)null!, 1));
        var client = new LegacyNativeGameClientRuntime { Player = null! };
        Assert.Throws<NullReferenceException>(() => AccountBan.AddBan(client, 1));
        Assert.Empty(AccountBan.BannedPoll);
    }
}
