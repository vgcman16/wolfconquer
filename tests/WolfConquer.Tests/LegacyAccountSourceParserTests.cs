using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed class LegacyAccountSourceParserTests
{
    [Fact]
    public void GuardShieldAccountConfigParserReadsLoaderAndMySqlSections()
    {
        var config = LegacyGuardShieldAccountConfigParser.ParseLines(
        [
            "[LoaderSystem]",
            "ServerIP=127.0.0.1",
            @"DbLocation=C:\fixtures\DBServer",
            "",
            "[MySql]",
            "Host=localhost",
            "Username=test_user",
            "Password=test_password",
            "Database=test_auth",
        ]);

        Assert.Equal("127.0.0.1", config.ServerIp);
        Assert.Equal(@"C:\fixtures\DBServer", config.DbLocation);
        Assert.True(config.HasMySqlSettings);
        Assert.Contains("Database=test_auth", config.ToMySqlConnectionString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AccServerLoginLogParserReadsSuccessfulLoginObservation()
    {
        const string line =
            "UserName[Acc] |hero| UIDAccount |1290001 | IPAccount | 10.0.0.5 |  PasswordAccount  [secret-fixture] [The Clock] 7:8:9";

        Assert.True(LegacyAccServerLoginLogParser.TryParseLine(line, out var entry));
        Assert.Equal("hero", entry.Username);
        Assert.Equal(1290001, entry.AccountId);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.Equal("secret-fixture", entry.Password);
        Assert.Equal("7:8:9", entry.ClockText);
    }

    [Fact]
    public async Task AccServerLogRepositoryUsesLatestObservationPerAccountId()
    {
        var repository = new LegacyAccServerLogAccountRepository(
        [
            new LegacyAccServerLoginLogEntry("hero", 1290001, "10.0.0.5", "old-pass", "1:0:0"),
            new LegacyAccServerLoginLogEntry("hero", 1290001, "10.0.0.5", "current-pass", "1:5:0"),
        ]);
        var service = new AccountLoginService(repository);

        var oldResult = await service.ValidateAsync(new AccountLoginRequest("hero", "old-pass"));
        var currentResult = await service.ValidateAsync(new AccountLoginRequest("hero", "current-pass"));

        Assert.Equal(AccountLoginStatus.WrongPassword, oldResult.Status);
        Assert.Equal(AccountLoginStatus.Success, currentResult.Status);
        Assert.Equal(1290001, currentResult.Account?.Id);
        Assert.Equal(1290001, currentResult.Account?.EntityId);
    }

    [Fact]
    public async Task AccServerLogRepositoryMarksBanUidAccountsAsBanned()
    {
        var now = new DateTime(2026, 6, 11, 12, 0, 0, DateTimeKind.Local);
        var started = now.AddMinutes(-15).Ticks;
        var bannedIds = LegacyBanUidFileParser.ParseActiveAccountIds(
        [
            "Count=1",
            $"1290002/2/{started}/fixture/Ban reason/",
        ], now);
        var repository = new LegacyAccServerLogAccountRepository(
        [
            new LegacyAccServerLoginLogEntry("bannedhero", 1290002, "10.0.0.6", "pass", "2:0:0"),
        ], bannedIds);
        var service = new AccountLoginService(repository);

        var result = await service.ValidateAsync(new AccountLoginRequest("bannedhero", "pass"));

        Assert.Equal(AccountLoginStatus.Banned, result.Status);
        Assert.Equal(1290002, result.Account?.Id);
    }
}
