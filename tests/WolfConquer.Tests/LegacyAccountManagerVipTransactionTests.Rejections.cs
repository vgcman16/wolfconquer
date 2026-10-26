using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerVipTransactionTests
{
    [WindowsNpcSaleTheory]
    [InlineData((byte)35, false)]
    [InlineData((byte)35, true)]
    [InlineData((byte)197, false)]
    [InlineData((byte)197, true)]
    public async Task ActualReadOrReplacementFailureCannotDebitOrGrantAndUnlockedRetrySucceeds(byte option, bool allowRead)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var files = fixture.Files();
        using (File.Open(fixture.UserPath, FileMode.Open, FileAccess.Read, allowRead ? FileShare.Read : FileShare.None))
        {
            await fixture.Purchase(option);
            Assert.Same(before, fixture.Current);
            Assert.DoesNotContain(fixture.Sent, IsSuccess);
        }
        fixture.AssertUnchanged(before, files, 0);

        await fixture.Purchase(option);

        var cost = option == 35 ? 30UL : 100UL;
        var level = option == 35 ? 6UL : 7UL;
        fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30),
            files[Path.Combine("Users", Uid.ToString(CultureInfo.InvariantCulture) + ".ini")]);
        AssertBurst(fixture.Sent.Where(IsSuccess).ToArray(), level);
    }

    [Theory]
    [InlineData("sessions")]
    [InlineData("trades")]
    [InlineData("recovery")]
    [InlineData("uninitialized-recovery")]
    [InlineData("users-root")]
    public async Task MissingGenuineTransactionOwnersCannotPurchase(string missing)
    {
        using var fixture = new Fixture();
        var session = missing == "users-root" ? fixture.Current with { UsersRoot = null } : fixture.Current;
        if (missing == "users-root") fixture.Sessions.Set(fixture.Connection, session);
        var context = fixture.Context;
        context = missing switch
        {
            "sessions" => context with { Sessions = null },
            "trades" => context with { Trades = null },
            "recovery" => context with { TradeRecovery = null },
            "uninitialized-recovery" => context with { TradeRecovery = new LegacyTradeRecoveryState(fixture.UsersRoot, null) },
            _ => context
        };
        var files = fixture.Files();

        await fixture.Purchase(35, context);

        fixture.AssertUnchanged(session, files, 0);
    }

    [Theory]
    [InlineData("DonatePoints", "149")]
    [InlineData("VipLevel", "4")]
    [InlineData("UID", "1299652")]
    public async Task AStaleCharacterFileCannotBeOverwrittenFromTheSession(string key, string value)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var old = before.Character.Attributes[key];
        var text = File.ReadAllText(fixture.UserPath).Replace(key + "=" + old + "\r\n", key + "=" + value + "\r\n", StringComparison.Ordinal);
        File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var files = fixture.Files();

        await fixture.Purchase(35);

        fixture.AssertUnchanged(before, files, 0);
    }

    [Theory]
    [InlineData((byte)35, 29UL)]
    [InlineData((byte)197, 99UL)]
    public async Task InsufficientDonationPointsCannotConsumeCpOrGrantVip(byte option, ulong points)
    {
        using var fixture = new Fixture(points: points);
        var before = fixture.Current;
        var files = fixture.Files();

        await fixture.Purchase(option);

        fixture.AssertUnchanged(before, files, 0);
        Assert.Contains(fixture.Sent, frame => LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var reply) &&
            reply.Text.Contains("donation points", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData((byte)35)]
    [InlineData((byte)197)]
    public async Task DateRangeOverflowRefusesWithoutChangingAnyManagedOrDurableField(byte option)
    {
        using var fixture = new Fixture(expiry: DateTime.MaxValue);
        var before = fixture.Current;
        var files = fixture.Files();

        await fixture.Purchase(option);

        fixture.AssertUnchanged(before, files, 0);
    }
}
