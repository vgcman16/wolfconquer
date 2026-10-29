using IpBan = WolfConquer.Core.Data.LegacyNativeSystemBanned;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBanPolicyTests
{
    [Fact]
    public void AddIpCapturesCurrentTicksAndDuplicateKeyPublishesANewRecord()
    {
        using var state = new State();
        var before = DateTime.Now.Ticks;
        IpBan.AddBan("127.0.0.1", 7);
        var after = DateTime.Now.Ticks;
        var first = Assert.Single(IpBan.BannedPoll).Value;
        Assert.Equal("127.0.0.1", first.IP);
        Assert.Equal(7u, first.Hours);
        Assert.InRange(first.StartBan, before, after);
        Assert.True(IpBan.BannedPoll.Update);
        IpBan.AddBan("127.0.0.1", 9);
        var second = Assert.Single(IpBan.BannedPoll).Value;
        Assert.NotSame(first, second);
        Assert.Equal(9u, second.Hours);
        Assert.Equal(7u, first.Hours);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("absent")]
    [InlineData(null)]
    public void OriginalIpRemoveBanIsAnActualNoOp(string? ip)
    {
        using var state = new State();
        var row = Ip("127.0.0.1");
        IpBan.BannedPoll.Add("127.0.0.1", row);
        var cache = IpBan.BannedPoll.GetValues();
        long revision = IpBan.BannedPoll.Revision;
        IpBan.RemoveBan(ip);
        Assert.Same(row, Assert.Single(IpBan.BannedPoll).Value);
        Assert.False(IpBan.BannedPoll.Update);
        Assert.Equal(cache, IpBan.BannedPoll.GetValues());
        Assert.Equal(revision, IpBan.BannedPoll.Revision);
    }

    [Fact]
    public void MissingIpReturnsFalseAndEmptyMessageWithoutMutation()
    {
        using var state = new State();
        Assert.False(IpBan.IsBanned("missing", out string message));
        Assert.Equal("", message);
        Assert.False(IpBan.BannedPoll.Update);
    }

    [Fact]
    public void ExpiredIpIsRemovedByItsStoredIpAndTheCacheIsInvalidated()
    {
        using var state = new State();
        IpBan.BannedPoll.Add("ip", Ip("ip"));
        IpBan.BannedPoll.GetValues();
        Assert.False(IpBan.IsBanned("ip", out string message));
        Assert.Equal("", message);
        Assert.Empty(IpBan.BannedPoll);
        Assert.True(IpBan.BannedPoll.Update);
        Assert.Empty(IpBan.BannedPoll.GetValues());
    }

    [Fact]
    public void ActiveIpUsesWholeNativeDayHourMinuteMessageAndDoesNotMutate()
    {
        using var state = new State();
        var row = Ip("ip", 4, DateTime.Now.AddDays(2).AddMinutes(70).Ticks);
        IpBan.BannedPoll.Add("ip", row);
        IpBan.BannedPoll.GetValues();
        DateTime before = DateTime.Now;
        Assert.True(IpBan.IsBanned("ip", out string message));
        DateTime after = DateTime.Now;
        AssertRemaining(message, DateTime.FromBinary(row.StartBan).AddHours(row.Hours), before, after, account: false);
        Assert.Same(row, IpBan.BannedPoll["ip"]);
        Assert.False(IpBan.BannedPoll.Update);
    }

    [Fact]
    public void ExpiredMismatchedIpRemovesTheStoredKeyNotTheLookupKey()
    {
        using var state = new State();
        var expired = Ip("other");
        IpBan.BannedPoll.Add("lookup", expired);
        IpBan.BannedPoll.Add("other", Ip("other", 1, DateTime.Now.Ticks));
        Assert.False(IpBan.IsBanned("lookup", out string message));
        Assert.Equal("", message);
        Assert.Same(expired, Assert.Single(IpBan.BannedPoll).Value);
        Assert.True(IpBan.BannedPoll.ContainsKey("lookup"));
        Assert.False(IpBan.BannedPoll.ContainsKey("other"));
    }
}
