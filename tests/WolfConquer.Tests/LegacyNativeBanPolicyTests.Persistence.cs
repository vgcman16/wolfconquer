using System.Globalization;
using WolfConquer.Core.World;
using IpBan = WolfConquer.Core.Data.LegacyNativeSystemBanned;
using AccountBan = WolfConquer.Core.Data.LegacyNativeSystemBannedAccount;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeBanPolicyTests
{
    [Fact]
    public void WholeSaveAndLoadRoundTripBothRealNativeBanFiles()
    {
        using var state = new State();
        using var files = new Files();
        var ip = Ip("127.0.0.1", 9, 638000000000000000);
        var account = Account(17, "Wolf雪", 12, 638000000000000123, "fixture");
        IpBan.BannedPoll.Add(ip.IP!, ip);
        AccountBan.BannedPoll.Add(account.UID, account);
        string ipPath = files.PathFor("BanIp.txt"), accountPath = files.PathFor("BanUID.txt");
        IpBan.Save();
        AccountBan.Save();
        Assert.Equal(new[] { "Count=1", "127.0.0.1/9/638000000000000000/" }, File.ReadAllLines(ipPath));
        Assert.Equal(new[] { "Count=1", "17/12/638000000000000123/Wolf雪/fixture/" }, File.ReadAllLines(accountPath));
        IpBan.BannedPoll.Clear();
        AccountBan.BannedPoll.Clear();
        IpBan.Load();
        AccountBan.Load();
        var loadedIp = Assert.Single(IpBan.BannedPoll).Value;
        var loadedAccount = Assert.Single(AccountBan.BannedPoll).Value;
        Assert.NotSame(ip, loadedIp);
        Assert.NotSame(account, loadedAccount);
        Assert.Equal(ip.ToString(), loadedIp.ToString());
        Assert.Equal(account.ToString(), loadedAccount.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SaveUsesOpenModeToReplacePriorBytesAndEmptySaveStillWritesItsHeader(bool account)
    {
        using var state = new State();
        using var files = new Files();
        string path = files.PathFor(account ? "BanUID.txt" : "BanIp.txt");
        File.WriteAllText(path, "old bytes far longer than the new empty header");
        if (account) AccountBan.Save(); else IpBan.Save();
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(path));
    }

    [Fact]
    public void ReloadRetainsUnmentionedKeysButDuplicateDiskKeysPublishTheFinalWholeRecord()
    {
        using var state = new State();
        using var files = new Files();
        var keptIp = Ip("keep");
        var oldIp = Ip("replace", 1);
        var keptAccount = Account(90, "Keep");
        var oldAccount = Account(17, "Old");
        IpBan.BannedPoll.Add("keep", keptIp);
        IpBan.BannedPoll.Add("replace", oldIp);
        AccountBan.BannedPoll.Add(90, keptAccount);
        AccountBan.BannedPoll.Add(17, oldAccount);
        files.Seed("BanIp.txt", "replace/2/100/", "replace/3/200/");
        files.Seed("BanUID.txt", "17/2/100/First/one/", "17/3/200/Final/two/");
        IpBan.Load();
        AccountBan.Load();
        Assert.Same(keptIp, IpBan.BannedPoll["keep"]);
        Assert.Same(keptAccount, AccountBan.BannedPoll[90]);
        Assert.NotSame(oldIp, IpBan.BannedPoll["replace"]);
        Assert.NotSame(oldAccount, AccountBan.BannedPoll[17]);
        Assert.Equal("replace/3/200/", IpBan.BannedPoll["replace"].ToString());
        Assert.Equal("17/3/200/Final/two/", AccountBan.BannedPoll[17].ToString());
        Assert.Equal(2, IpBan.BannedPoll.Count);
        Assert.Equal(2, AccountBan.BannedPoll.Count);
    }

    [Fact]
    public void LoadUsesNativeTypedParserFallbacksAndDoesNotValidateDatesOrHours()
    {
        using var state = new State();
        using var files = new Files();
        files.Seed("BanIp.txt", "bad/not-uint/not-long/", "wide/4294967295/-9223372036854775808/");
        files.Seed("BanUID.txt", "bad/-1/overflow/RawName/RawReason/", "4294967295/4294967295/9223372036854775807/Wide/Reason/");
        IpBan.Load();
        AccountBan.Load();
        Assert.Equal("bad/0/0/", IpBan.BannedPoll["bad"].ToString());
        Assert.Equal("wide/4294967295/-9223372036854775808/", IpBan.BannedPoll["wide"].ToString());
        Assert.Equal("0/0/0/RawName/RawReason/", AccountBan.BannedPoll[0].ToString());
        Assert.Equal("4294967295/4294967295/9223372036854775807/Wide/Reason/",
            AccountBan.BannedPoll[uint.MaxValue].ToString());
    }

    [Fact]
    public void UnescapedSlashFieldsShiftTheExactNativeReadCursor()
    {
        using var state = new State();
        using var files = new Files();
        files.Seed("BanIp.txt", "ip/part/9/100/");
        files.Seed("BanUID.txt", "17/9/100/Wolf/Name/Reason/Tail/");
        IpBan.Load();
        AccountBan.Load();
        Assert.Equal("ip/0/9/", IpBan.BannedPoll["ip"].ToString());
        Assert.Equal("17/9/100/Wolf/Name/", AccountBan.BannedPoll[17].ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CountBoundsTheRealLoaderAndIgnoresTrailingPhysicalRows(bool account)
    {
        using var state = new State();
        using var files = new Files();
        string path = files.PathFor(account ? "BanUID.txt" : "BanIp.txt");
        File.WriteAllLines(path, account
            ? new[] { "Count=1", "17/4/100/Wolf/reason/", "18/5/200/Ignored/reason/" }
            : new[] { "Count=1", "ip/4/100/", "ignored/5/200/" });
        if (account)
        {
            AccountBan.Load();
            Assert.Equal("17/4/100/Wolf/reason/", Assert.Single(AccountBan.BannedPoll).Value.ToString());
        }
        else
        {
            IpBan.Load();
            Assert.Equal("ip/4/100/", Assert.Single(IpBan.BannedPoll).Value.ToString());
        }
    }

    [Fact]
    public void PrematureEofAndDelimiterlessRowsBecomeNativeDefaultRecords()
    {
        using var state = new State();
        using var files = new Files();
        File.WriteAllLines(files.PathFor("BanIp.txt"), new[] { "Count=3", "actual/2/100/", "delimiterless" });
        File.WriteAllLines(files.PathFor("BanUID.txt"), new[] { "Count=3", "17/2/100/Wolf/reason/", "delimiterless" });
        IpBan.Load();
        AccountBan.Load();
        Assert.Equal(2, IpBan.BannedPoll.Count);
        Assert.Equal(2, AccountBan.BannedPoll.Count);
        Assert.Equal("/0/0/", IpBan.BannedPoll[""].ToString());
        Assert.Equal("0/0/0///", AccountBan.BannedPoll[0].ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroHeaderDoesNotClearExistingPublishedRecords(bool account)
    {
        using var state = new State();
        using var files = new Files();
        files.Seed(account ? "BanUID.txt" : "BanIp.txt");
        if (account)
        {
            var row = Account(17);
            AccountBan.BannedPoll.Add(17, row);
            AccountBan.BannedPoll.GetValues();
            AccountBan.Load();
            Assert.Same(row, Assert.Single(AccountBan.BannedPoll).Value);
            Assert.False(AccountBan.BannedPoll.Update);
        }
        else
        {
            var row = Ip("ip");
            IpBan.BannedPoll.Add("ip", row);
            IpBan.BannedPoll.GetValues();
            IpBan.Load();
            Assert.Same(row, Assert.Single(IpBan.BannedPoll).Value);
            Assert.False(IpBan.BannedPoll.Update);
        }
    }

    [Fact]
    public void SaveConcatenatesTheRealConfigPrefixWithoutInsertingASeparator()
    {
        using var state = new State();
        using var files = new Files();
        LegacyNativeServerConfig.DbLocation = Path.Combine(files.Root, "prefix-");
        string ip = files.PathFor("prefix-BanIp.txt"), account = files.PathFor("prefix-BanUID.txt");
        IpBan.Save();
        AccountBan.Save();
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(ip));
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(account));
        Assert.False(Directory.Exists(Path.Combine(files.Root, "prefix-")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullRecordSaveFaultsBeforeOpeningOrTruncatingTheDestination(bool account)
    {
        using var state = new State();
        using var files = new Files();
        string path = files.PathFor(account ? "BanUID.txt" : "BanIp.txt");
        File.WriteAllText(path, "unchanged bytes");
        if (account)
        {
            AccountBan.BannedPoll.Add(17, null!);
            Assert.Throws<NullReferenceException>(() => AccountBan.Save());
        }
        else
        {
            IpBan.BannedPoll.Add("ip", null!);
            Assert.Throws<NullReferenceException>(() => IpBan.Save());
        }
        Assert.Equal("unchanged bytes", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingParentSaveIsNotCreatedOrSwallowed(bool account)
    {
        using var state = new State();
        using var files = new Files();
        string missing = Path.Combine(files.Root, "missing");
        LegacyNativeServerConfig.DbLocation = missing + Path.DirectorySeparatorChar;
        if (account) Assert.Throws<DirectoryNotFoundException>(() => AccountBan.Save());
        else Assert.Throws<DirectoryNotFoundException>(() => IpBan.Save());
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LockedExistingSavePropagatesTheRealFileFaultWithoutTruncation(bool account)
    {
        using var state = new State();
        using var files = new Files();
        string path = files.PathFor(account ? "BanUID.txt" : "BanIp.txt");
        File.WriteAllText(path, "unchanged bytes");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (account) Assert.ThrowsAny<IOException>(() => AccountBan.Save());
            else Assert.ThrowsAny<IOException>(() => IpBan.Save());
        }
        Assert.Equal("unchanged bytes", File.ReadAllText(path));
    }

    [Fact]
    public void SaveAndLoadUseCurrentCultureRatherThanAnInventedInvariantParser()
    {
        using var state = new State();
        using var files = new Files();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        CultureInfo.CurrentCulture = culture;
        IpBan.BannedPoll.Add("ip", Ip("ip", 1, -31));
        AccountBan.BannedPoll.Add(17, Account(17, start: -37));
        string ip = files.PathFor("BanIp.txt"), account = files.PathFor("BanUID.txt");
        IpBan.Save();
        AccountBan.Save();
        Assert.Contains("~31", File.ReadAllText(ip));
        Assert.Contains("~37", File.ReadAllText(account));
        IpBan.BannedPoll.Clear();
        AccountBan.BannedPoll.Clear();
        IpBan.Load();
        AccountBan.Load();
        Assert.Equal(-31L, IpBan.BannedPoll["ip"].StartBan);
        Assert.Equal(-37L, AccountBan.BannedPoll[17].StartBan);
    }
}
