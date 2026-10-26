using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerVipTransactionTests
{
    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task PurchaseCommitsCompleteDebitAndVipImageBeforeItsFirstSuccessFrame(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture();
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);
        var observed = 0;
        Exception? observationFailure = null;
        fixture.BeforeSend = frame =>
        {
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) ||
                !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.VIPLevel)) return;
            observed++;
            observationFailure = Record.Exception(() =>
                fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes));
        };

        await fixture.Purchase(option);

        Assert.Equal(1, observed);
        Assert.Null(observationFailure);
        fixture.AssertCommitted(before, 150 - cost, level, FutureExpiry.AddDays(30), bytes);
        AssertBurst(fixture.Sent.ToArray(), level);
    }

    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task ExpiredVipExtendsFromTheExplicitClockRatherThanOldExpiry(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture(expiry: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local));
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);

        await fixture.Purchase(option);

        fixture.AssertCommitted(before, 150 - cost, level, ControlledClock.LocalNow.AddDays(30), bytes);
        AssertBurst(fixture.Sent.ToArray(), level);
    }

    [Theory]
    [InlineData((byte)35, 30UL, 6UL)]
    [InlineData((byte)197, 100UL, 7UL)]
    public async Task FreshIdenticalRequestBuysAnotherPaidExtensionButOldCapturedContextCannotReplay(byte option, ulong cost, ulong level)
    {
        using var fixture = new Fixture(points: cost * 3);
        var original = fixture.Current;
        var originalBytes = File.ReadAllBytes(fixture.UserPath);
        var staleContext = fixture.Context;
        await fixture.Purchase(option, staleContext);
        fixture.AssertCommitted(original, cost * 2, level, FutureExpiry.AddDays(30), originalBytes);
        var first = fixture.Current;
        var firstFiles = fixture.Files();
        var firstCount = fixture.Sent.Count;

        await fixture.Purchase(option, staleContext);

        fixture.AssertUnchanged(first, firstFiles, firstCount);
        var firstBytes = File.ReadAllBytes(fixture.UserPath);
        await fixture.Purchase(option); // New entry captures the Store's actual current session.
        fixture.AssertCommitted(first, cost, level, FutureExpiry.AddDays(60), firstBytes);
        Assert.Equal(6, fixture.Sent.Count);
        AssertBurst(fixture.Sent.Take(3).ToArray(), level);
        AssertBurst(fixture.Sent.Skip(3).ToArray(), level);
    }

    [Fact]
    public async Task OneCharacterFilePurchaseDoesNotInventAnItemRootRecoveryPrerequisite()
    {
        using var fixture = new Fixture(withItemsAuthority: false);
        var before = fixture.Current;
        var bytes = File.ReadAllBytes(fixture.UserPath);

        await fixture.Purchase(35, fixture.Context with { PlayerItemsRoot = null });

        fixture.AssertCommitted(before, 120, 6, FutureExpiry.AddDays(30), bytes);
        AssertBurst(fixture.Sent.ToArray(), 6);
    }

    private static void AssertBurst(byte[][] frames, ulong level)
    {
        Assert.Equal(3, frames.Length);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[0]), out var update));
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.VIPLevel, entry.Type);
        Assert.Equal(level, entry.Value);
        Assert.True(LegacyVipStatusPacket.TryParse(ReadFrame(frames[1]), out var status));
        Assert.Equal(LegacyVipStatusFlags.FullVip, status.Flags);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frames[2]), out var reply));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("You have received VIP" + level.ToString(CultureInfo.InvariantCulture) + " (30 days).", reply.Text);
    }
}
