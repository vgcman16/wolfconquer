using MySqlConnector;
using WolfConquer.Core.Accounts;

namespace WolfConquer.Tests;

public sealed class LegacyGuardShieldConnectionSafetyTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData(" localhost ")]
    [InlineData("127.0.0.1")]
    [InlineData("127.7.8.9")]
    [InlineData("::1")]
    public void ExplicitLoopbackKeepsLocalDevelopmentOptions(string host)
    {
        var config = Valid() with { MySqlHost = host };
        var parsed = new MySqlConnectionStringBuilder(config.ToMySqlConnectionString());
        Assert.Equal(host.Trim(), parsed.Server);
        Assert.Equal(3306U, parsed.Port);
        Assert.Equal(MySqlSslMode.None, parsed.SslMode);
        Assert.True(parsed.AllowPublicKeyRetrieval);
        Assert.Equal(config.MySqlPassword, parsed.Password);
    }

    [Theory]
    [InlineData("db.example.invalid")]
    [InlineData("192.168.1.5")]
    [InlineData("58.218.200.113")]
    [InlineData("0.0.0.0")]
    [InlineData("localhost,db.example.invalid")]
    [InlineData("localhost.")]
    public void OtherEndpointsUseCertificateValidationWithoutPublicKeyFallback(string host)
    {
        var parsed = new MySqlConnectionStringBuilder((Valid() with { MySqlHost = host }).ToMySqlConnectionString());
        Assert.Equal(host, parsed.Server);
        Assert.Equal(MySqlSslMode.VerifyFull, parsed.SslMode);
        Assert.False(parsed.AllowPublicKeyRetrieval);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("database")]
    [InlineData("username")]
    [InlineData("password")]
    public void QuotedFieldsRemainLiteralAndCannotInjectConnectionOptions(string field)
    {
        const string value = "literal;Server=attacker.example.invalid;SslMode=None;Password=\"quoted;part\"";
        var original = Valid() with { MySqlHost = "db.example.invalid" };
        var changed = field switch
        {
            "host" => original with { MySqlHost = value },
            "database" => original with { MySqlDatabase = value },
            "username" => original with { MySqlUsername = value },
            "password" => original with { MySqlPassword = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        var parsed = new MySqlConnectionStringBuilder(changed.ToMySqlConnectionString());
        Assert.Equal(changed.MySqlHost, parsed.Server);
        Assert.Equal(changed.MySqlDatabase, parsed.Database);
        Assert.Equal(changed.MySqlUsername, parsed.UserID);
        Assert.Equal(changed.MySqlPassword, parsed.Password);
        Assert.Equal(3306U, parsed.Port);
        Assert.Equal(MySqlSslMode.VerifyFull, parsed.SslMode);
        Assert.False(parsed.AllowPublicKeyRetrieval);
    }

    [Fact]
    public void MissingRequiredStateRefusesAndEmptyPasswordRemainsExplicitlySupported()
    {
        foreach (var config in new[]
        {
            Valid() with { MySqlHost = null! }, Valid() with { MySqlHost = " " },
            Valid() with { MySqlUsername = "" }, Valid() with { MySqlDatabase = " " },
            Valid() with { MySqlPassword = null! },
        })
            Assert.Throws<InvalidOperationException>(() => config.ToMySqlConnectionString());
        var empty = new MySqlConnectionStringBuilder((Valid() with { MySqlPassword = "" }).ToMySqlConnectionString());
        Assert.Equal("", empty.Password);
        Assert.Equal("fixture-db", empty.Database);
    }

    [Fact]
    public void RecordTextDoesNotRevealItsCredentialOrEndpointFields()
    {
        var config = Valid(); var text = config.ToString();
        Assert.Contains(nameof(LegacyGuardShieldAccountConfig), text, StringComparison.Ordinal);
        foreach (var field in new[] { config.MySqlPassword, config.MySqlUsername, config.MySqlHost, config.MySqlDatabase })
            Assert.DoesNotContain(field, text, StringComparison.Ordinal);
        Assert.Contains("HasMySqlSettings = True", text, StringComparison.Ordinal);
    }

    private static LegacyGuardShieldAccountConfig Valid() =>
        new("fixture-server", "fixture-path", "localhost", "fixture-user", "fixture-secret", "fixture-db");
}
