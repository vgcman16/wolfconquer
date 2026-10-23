using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerAddFlagTests
{
    [Theory]
    [InlineData("rune")] [InlineData("rune-owner")] [InlineData("equipment")] [InlineData("metadata")]
    public Task DuplicateWithInvalidOptionalRunePreservesExistingXpAndRegistration(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        NativeAddFlagTestFixture.Ready(f.Owner); f.Rune();
        Assert.True(f.Player.BitVector.TryAdd((int)Flag.Superman, 123, false, 0));
        f.Player.XPBooster = 99; f.Player.XPKiller = 88;
        if (missing == "rune") f.Owner.Rune = null;
        else if (missing == "rune-owner") f.Runes.Owner = null;
        else if (missing == "equipment") f.Owner.Equipment = null;
        else LegacyNativePoolItems.ItemsBase!.Clear();
        var before = CaptureAdmission(f);
        Assert.False(f.Player.AddFlag(Flag.Superman, 999, true));
        AssertAdmissionUnchanged(f, before);
    });

    [Theory]
    [InlineData("rune")] [InlineData("rune-owner")] [InlineData("equipment")] [InlineData("metadata")]
    public Task FirstXpAdmissionWithInvalidOptionalRuneCommitsZeroBonusAndRealStatus(string missing)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture(); NativeAddFlagTestFixture.Ready(f.Owner); f.Rune();
        f.Player.XPBooster = 99; f.Player.XPKiller = 88;
        if (missing == "rune") f.Owner.Rune = null;
        else if (missing == "rune-owner") f.Runes.Owner = null;
        else if (missing == "equipment") f.Owner.Equipment = null;
        else LegacyNativePoolItems.ItemsBase!.Clear();
        Assert.True(f.Player.AddFlag(Flag.Superman, 20, true));
        Assert.Equal((byte)0, f.Player.XPBooster); Assert.Equal((byte)0, f.Player.XPKiller);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Superman), Assert.Single(f.Queue));
        Assert.Equal(20, f.Row(Flag.Superman).Secounds);
    });

    [Theory]
    [InlineData(6)] [InlineData(52)]
    public Task NullViewRefusesBeforeRegistrationBitmapXpOrOwnerPublication(int index)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture(); f.Player.View = null!;
        f.Player.XPBooster = 99; f.Player.XPKiller = 88;
        var before = CaptureAdmission(f);
        Assert.False(f.Player.AddFlag((Flag)index, 30, true));
        AssertAdmissionUnchanged(f, before);
    });

    [Fact]
    public Task ForeignViewRefusesWithoutPublishingToEitherGenuineOwner()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        var otherQueue = new Queue<byte[]>();
        var other = NativeSpellTestFixture.CreateOwner(otherQueue);
        other.Player.UID = 999; f.Player.View = other.Player.View;
        var before = CaptureAdmission(f);
        Assert.False(f.Player.AddFlag(Flag.Accelerated, 77, false, 0, 11, 22));
        AssertAdmissionUnchanged(f, before); Assert.Empty(otherQueue);
        Assert.Same(f.Owner, f.Player.Owner); Assert.Same(other, f.Player.View.Owner);
        Assert.Equal(17u, f.Player.UID); Assert.Equal(999u, other.Player.UID);
    });
}
