using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySecondaryPasswordStateTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 13, 7, 25, DateTimeKind.Local);
    private static readonly DateTime Deadline = new(2026, 10, 28, 13, 7, 25, DateTimeKind.Unspecified);

    [Fact]
    public void MissingMetadataIsUnprotectedAndNativeTrailingCommaRoundTrips()
    {
        Assert.True(LegacySecondaryPasswordState.TryRead(new Dictionary<string, string>(), out var missing, out var reason), reason);
        Assert.Equal(new LegacySecondaryPasswordState(0, 0, default), missing);
        Assert.Equal("0,0,0,", missing.ToLegacyText());
        var state = new LegacySecondaryPasswordState(1234, 1, Deadline);
        Assert.Equal("1234,1," + Deadline.Ticks.ToString(CultureInfo.InvariantCulture) + ",", state.ToLegacyText());
        Assert.True(Read(state.ToLegacyText(), out var loaded, out reason), reason);
        Assert.Equal(state, loaded);
        Assert.Equal(DateTimeKind.Unspecified, loaded.ResetAt.Kind);
        Assert.True(Read(state.ToLegacyText()[..^1], out loaded, out reason), reason);
        Assert.Equal(state, loaded);
    }

    [Theory]
    [InlineData("0,0,0", 0, 0)]
    [InlineData("4294967295,4294967295,ignored,", uint.MaxValue, uint.MaxValue)]
    [InlineData("1234,0,not-a-date", 1234, 0)]
    [InlineData("1234,2,9223372036854775808,", 1234, 2)]
    [InlineData("1234,0,", 1234, 0)]
    [InlineData(" +1234 , 0 ,ignored", 1234, 0)]
    public void NonResetStateIgnoresThirdFieldAndPreservesNativeNumericParsing(string text, uint password, uint reset)
    {
        Assert.True(Read(text, out var state, out var reason), reason);
        Assert.Equal(password, state.Password);
        Assert.Equal(reset, state.OnReset);
        Assert.Equal(DateTime.MinValue, state.ResetAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("1234,0")]
    [InlineData("invalid,0,0")]
    [InlineData("-1,0,0")]
    [InlineData("4294967296,0,0")]
    [InlineData("1234,invalid,0")]
    [InlineData("1234,4294967296,0")]
    [InlineData("1234,1,invalid")]
    [InlineData("1234,1,9223372036854775808")]
    [InlineData("1234,1,3155378976000000000")]
    [InlineData("1234,1,")]
    [InlineData("1234,0,0,extra")]
    [InlineData("1234,0,0,,")]
    public void PresentMalformedStorageNeverSilentlyGrantsAnUnprotectedState(string text)
    {
        Assert.False(Read(text, out _, out var reason));
        Assert.NotEmpty(reason);
        Assert.DoesNotContain(text.Length > 0 ? text : "secret", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateCaseInsensitiveMetadataAndNullMetadataFailClosed()
    {
        var duplicate = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["SecurityPass"] = "1234,0,0", ["securitypass"] = "0,0,0" };
        Assert.False(LegacySecondaryPasswordState.TryRead(duplicate, out _, out _));
        Assert.False(LegacySecondaryPasswordState.TryRead(new Dictionary<string, string> { ["SecurityPass"] = null! }, out _, out _));
    }

    [Fact]
    public void ActiveResetParsesDateTimeFromBinaryRatherThanTreatingItAsTicks()
    {
        var utc = new DateTime(2026, 10, 28, 13, 7, 25, DateTimeKind.Utc);
        Assert.True(Read("1234,1," + utc.ToBinary().ToString(CultureInfo.InvariantCulture), out var state, out var reason), reason);
        Assert.Equal(DateTimeKind.Utc, state.ResetAt.Kind);
        Assert.Equal(utc, state.ResetAt);
        Assert.Equal("1234,1," + utc.Ticks.ToString(CultureInfo.InvariantCulture) + ",", state.ToLegacyText());
    }

    [Fact]
    public void ResetDeadlineIsStrictlyGreaterAndExpiryRetainsTheDeadline()
    {
        var state = new LegacySecondaryPasswordState(1234, 1, Deadline);
        Assert.Equal(state, state.LoadAt(Deadline.AddTicks(-1), out var beforeExpired));
        Assert.False(beforeExpired);
        Assert.Equal(state, state.LoadAt(Deadline, out var atExpired));
        Assert.False(atExpired);
        var after = state.LoadAt(Deadline.AddTicks(1), out var afterExpired);
        Assert.True(afterExpired);
        Assert.Equal(new LegacySecondaryPasswordState(0, 0, Deadline), after);
        Assert.Equal(Deadline, after.ResetAt);
        Assert.Equal(new LegacySecondaryPasswordState(0, 0, default), after.LoadAt(Deadline.AddDays(1), out var secondExpired));
        Assert.False(secondExpired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(uint.MaxValue)]
    public void NonResetLoadIgnoresStoredDeadlineWithoutClearingPasswordOrResetValue(uint onReset)
    {
        var state = new LegacySecondaryPasswordState(1234, onReset, Deadline);
        Assert.Equal(new LegacySecondaryPasswordState(1234, onReset, default), state.LoadAt(Now, out var expired));
        Assert.False(expired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerifyComparesNewPasswordAndWrongAttemptDoesNotRevokePriorVerification(bool verified)
    {
        var state = new LegacySecondaryPasswordState(1234, 1, Deadline);
        var wrong = Apply(state, new(1, 1234, 9999), verified);
        Assert.Equal(state, wrong.State);
        Assert.Equal(verified, wrong.IsChecked);
        Assert.Equal(new LegacySecondaryPasswordPacket(6, 0, 0), wrong.Reply);
        Assert.Empty(wrong.Message);
        var correct = Apply(state, new(1, 9999, 1234), verified);
        Assert.Equal(state, correct.State);
        Assert.True(correct.IsChecked);
        AssertSuccess(correct, "Secondary verified!");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForgetSchedulesExactlySevenDaysAndKeepsVerificationAndPassword(bool verified)
    {
        var state = new LegacySecondaryPasswordState(1234, 0, default);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var result = Apply(state, new(2, 0, 0), verified);
            Assert.Equal(1234u, result.State.Password);
            Assert.Equal(1u, result.State.OnReset);
            Assert.Equal(Now.AddDays(7), result.State.ResetAt);
            Assert.Equal(DateTimeKind.Local, result.State.ResetAt.Kind);
            Assert.Equal(TimeSpan.FromDays(7).Ticks, result.State.ResetAt.Ticks - Now.Ticks);
            Assert.Equal(verified, result.IsChecked);
            AssertSuccess(result, "Your secondary password will be removed on 28/10/2026 (13:07).");
            var renewed = Apply(result.State, new(2, 1234, 9876), verified, Now.AddDays(1));
            Assert.Equal(Now.AddDays(8), renewed.State.ResetAt);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void SettingOnlyWhenUnprotectedPreservesResetMetadataAndDoesNotIssueVerification(uint action, bool verified)
    {
        var state = new LegacySecondaryPasswordState(0, 1, Deadline);
        var result = Apply(state, new(action, 9999, 5678), verified);
        Assert.Equal(new LegacySecondaryPasswordState(5678, 1, Deadline), result.State);
        Assert.Equal(verified, result.IsChecked);
        AssertSuccess(result, "Successfully set! Please remember your secondary password");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModifyChecksOldPasswordClearsResetFlagRetainsDateAndDoesNotIssueVerification(bool verified)
    {
        var state = new LegacySecondaryPasswordState(1234, 1, Deadline);
        var wrong = Apply(state, new(4, 9999, 5678), verified);
        Assert.Equal(state, wrong.State);
        Assert.Equal(verified, wrong.IsChecked);
        Assert.Equal(new LegacySecondaryPasswordPacket(6, 1, 0), wrong.Reply);
        Assert.Empty(wrong.Message);
        var modified = Apply(state, new(4, 1234, 5678), verified);
        Assert.Equal(new LegacySecondaryPasswordState(5678, 0, Deadline), modified.State);
        Assert.Equal(verified, modified.IsChecked);
        AssertSuccess(modified, "Successfully modified! Please remember your secondary password");
        var removed = Apply(state, new(4, 1234, 0), verified);
        Assert.Equal(new LegacySecondaryPasswordState(0, 0, Deadline), removed.State);
        Assert.Equal(verified, removed.IsChecked);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 1234)]
    [InlineData(5, 1234)]
    [InlineData(6, 1234)]
    [InlineData(99, 1234)]
    public void IgnoredActionsReturnNoTransitionOrReply(uint action, uint password)
    {
        Assert.Null(new LegacySecondaryPasswordState(password, 0, default).Apply(new(action, 0, 0), false, Now));
    }

    [Fact]
    public void ApplyDoesNotAutomaticallyExpireResetOutsideTheLoadPath()
    {
        var state = new LegacySecondaryPasswordState(1234, 1, Deadline);
        var verified = Apply(state, new(1, 0, 1234), false, Deadline.AddTicks(1));
        Assert.Equal(state, verified.State);
        Assert.True(verified.IsChecked);
    }

    [Fact]
    public void ForgetRetainsNativeDateOverflowInsteadOfInventingAShorterResetWindow()
    {
        var state = new LegacySecondaryPasswordState(1234, 0, default);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Apply(new(2, 0, 0), false, DateTime.MaxValue));
    }

    private static bool Read(string text, out LegacySecondaryPasswordState state, out string reason) =>
        LegacySecondaryPasswordState.TryRead(new Dictionary<string, string> { ["SecurityPass"] = text }, out state, out reason);

    private static LegacySecondaryPasswordTransition Apply(LegacySecondaryPasswordState state,
        LegacySecondaryPasswordPacket packet, bool verified, DateTime? now = null) =>
        Assert.IsType<LegacySecondaryPasswordTransition>(state.Apply(packet, verified, now ?? Now));

    private static void AssertSuccess(LegacySecondaryPasswordTransition transition, string message)
    {
        Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0), transition.Reply);
        Assert.Equal(message, transition.Message);
    }
}
