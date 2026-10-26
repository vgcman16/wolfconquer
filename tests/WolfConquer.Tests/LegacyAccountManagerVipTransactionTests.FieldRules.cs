using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerVipTransactionTests
{
    [Theory]
    [InlineData("DonatePoints", "-1")]
    [InlineData("DonatePoints", "18446744073709551616")]
    [InlineData("VipLevel", "256")]
    [InlineData("VipTime", "not-a-time")]
    [InlineData("VipTime", "9223372036854775807")]
    public async Task MalformedOwnedEconomicStateCannotBecomeAFreeOrRestartedPurchase(string key, string value)
    {
        using var fixture = new Fixture();
        RewriteAndReload(fixture, text => text.Replace(key + "=" + fixture.Current.Character.Attributes[key] + "\r\n",
            key + "=" + value + "\r\n", StringComparison.Ordinal));
        var before = fixture.Current;
        var files = fixture.Files();

        await fixture.Purchase(35);

        fixture.AssertUnchanged(before, files, 0);
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData((byte)35)]
    [InlineData((byte)197)]
    public async Task RealClockBoundaryDiskChangeCannotBeOverwrittenByThePreparedPurchase(byte option)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        Dictionary<string, byte[]>? changedFiles = null;
        var called = 0;
        fixture.Clock.BeforeRead = () =>
        {
            fixture.Clock.BeforeRead = null;
            called++;
            var text = File.ReadAllText(fixture.UserPath).Replace("KeepBytes=outside-the-three-vip-fields",
                "KeepBytes=changed-by-another-real-writer", StringComparison.Ordinal);
            File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
            changedFiles = fixture.Files();
        };

        await fixture.Purchase(option);

        Assert.Equal(1, called);
        fixture.AssertUnchanged(before, Assert.IsType<Dictionary<string, byte[]>>(changedFiles), 0);
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealClockBoundaryEconomicMutationInvalidatesTheCapturedSession(bool remove)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var files = fixture.Files();
        var fields = Assert.IsType<Dictionary<string, string>>(before.Character.Attributes);
        fixture.Clock.BeforeRead = () =>
        {
            fixture.Clock.BeforeRead = null;
            if (remove) Assert.True(fields.Remove("DonatePoints"));
            else fields["DonatePoints"] = "149";
        };

        await fixture.Purchase(35);

        Assert.Same(before, fixture.Current);
        Assert.Equal(remove ? null : "149", fields.GetValueOrDefault("DonatePoints"));
        fixture.AssertFiles(files);
        Assert.Empty(fixture.Sent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingVipTimeUsesTheEstablishedFallbackAndAppendsOnlyThatField(bool legacyExpiry)
    {
        using var fixture = new Fixture();
        RewriteAndReload(fixture, text =>
        {
            text = text.Replace("VipTime=" + FutureExpiry.ToBinary().ToString(CultureInfo.InvariantCulture) + "\r\n",
                string.Empty, StringComparison.Ordinal);
            return legacyExpiry ? text.Replace("KeepBytes=", "ExpireVip=" +
                FutureExpiry.ToBinary().ToString(CultureInfo.InvariantCulture) + "\r\nKeepBytes=", StringComparison.Ordinal) : text;
        });
        var before = fixture.Current;
        var files = fixture.Files();
        var expiry = (legacyExpiry ? FutureExpiry : ControlledClock.LocalNow).AddDays(30);
        var expected = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.UserPath))
            .Replace("DonatePoints=150\r\n", "DonatePoints=120\r\n", StringComparison.Ordinal)
            .Replace("VipLevel=0\r\n", "VipLevel=6\r\n", StringComparison.Ordinal)
            .Replace("[FixtureOtherSection]", "VipTime=" + expiry.ToBinary().ToString(CultureInfo.InvariantCulture) +
                "\r\n[FixtureOtherSection]", StringComparison.Ordinal);
        files[Path.Combine("Users", Uid.ToString(CultureInfo.InvariantCulture) + ".ini")] = Encoding.UTF8.GetBytes(expected);

        await fixture.Purchase(35);

        Assert.NotSame(before, fixture.Current);
        Assert.Equal("120", fixture.Current.Character.Attributes["DonatePoints"]);
        Assert.Equal("6", fixture.Current.Character.Attributes["VipLevel"]);
        Assert.Equal(expiry.ToBinary().ToString(CultureInfo.InvariantCulture), fixture.Current.Character.Attributes["VipTime"]);
        Assert.Equal(legacyExpiry, fixture.Current.Character.Attributes.ContainsKey("ExpireVip"));
        fixture.AssertFiles(files);
        AssertBurst(fixture.Sent.ToArray(), 6);
    }

    [Fact]
    public async Task UnsavedUnrelatedRuntimeLocationDoesNotBlockTheEconomicTransaction()
    {
        using var fixture = new Fixture();
        var moved = fixture.Current with { Character = fixture.Current.Character with { X = 431, Y = 381 } };
        fixture.Sessions.Set(fixture.Connection, moved);
        var bytes = File.ReadAllBytes(fixture.UserPath);

        await fixture.Purchase(35);

        fixture.AssertCommitted(moved, 120, 6, FutureExpiry.AddDays(30), bytes);
        Assert.Equal(431L, fixture.Current.Character.X);
        Assert.Equal(381L, fixture.Current.Character.Y);
        var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.UserPath));
        Assert.Equal(430L, saved.X);
        Assert.Equal(380L, saved.Y);
        AssertBurst(fixture.Sent.ToArray(), 6);
    }

    [Fact]
    public async Task ValidManagedDonationBalanceIsNotRestrictedToAnUnprovedNativeWidth()
    {
        using var fixture = new Fixture(points: ulong.MaxValue);
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);

        await fixture.Purchase(197);

        fixture.AssertCommitted(before, ulong.MaxValue - 100, 7, FutureExpiry.AddDays(30), bytes);
        AssertBurst(fixture.Sent.ToArray(), 7);
    }

    [Fact]
    public async Task ForeignCharacterFileBindingCannotAuthorizeTheCurrentAccountPurchase()
    {
        using var fixture = new Fixture();
        var before = fixture.Current with { Character = fixture.Current.Character with { SourceFile = "1299652.ini" } };
        fixture.Sessions.Set(fixture.Connection, before);
        var files = fixture.Files();

        await fixture.Purchase(35);

        fixture.AssertUnchanged(before, files, 0);
        Assert.Empty(fixture.Sent);
    }

    [Fact]
    public async Task ChangedSavedExpiryCannotBeSilentlyResetFromTheCapturedSession()
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var text = File.ReadAllText(fixture.UserPath).Replace("VipTime=" +
            FutureExpiry.ToBinary().ToString(CultureInfo.InvariantCulture), "VipTime=" +
            FutureExpiry.AddDays(1).ToBinary().ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
        var files = fixture.Files();

        await fixture.Purchase(35);

        fixture.AssertUnchanged(before, files, 0);
        Assert.Empty(fixture.Sent);
    }

    private static void RewriteAndReload(Fixture fixture, Func<string, string> transform)
    {
        var text = transform(File.ReadAllText(fixture.UserPath));
        File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
        var current = fixture.Current;
        fixture.Sessions.Set(fixture.Connection, current with
        {
            Character = CharacterSnapshotParser.ParseText(text, Path.GetFileName(fixture.UserPath)),
        });
    }
}
