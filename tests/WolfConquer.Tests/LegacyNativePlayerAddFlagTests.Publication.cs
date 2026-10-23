using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerAddFlagTests
{
    [Fact]
    public Task ExactCapacityRealRentalsPublishBothCompleteGoldenFrames()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        using var rentals = new AdmissionPoolFault("exact");
        Assert.True(f.Player.AddFlag(Flag.Accelerated, -3, true, 5, uint.MaxValue, 3));
        var frames = f.Queue.ToArray(); Assert.Equal(2, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Accelerated), frames[0]);
        Assert.Equal(NativeAddFlagTestFixture.Timed(Flag.Accelerated, -3, uint.MaxValue, 3), frames[1]);
    });

    [Theory]
    [InlineData("missing")] [InlineData("null")] [InlineData("small")] [InlineData("disposed")]
    [InlineData("alias")] [InlineData("second-small")] [InlineData("second-disposed")]
    public Task TimedPreparationFailurePreservesNonemptyPriorRegistrationAndExactPriorFrames(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        Assert.True(f.Player.AddFlag(Flag.TeamLeader, 20, true));
        f.Player.XPBooster = 91; f.Player.XPKiller = 81;
        var before = CaptureAdmission(f);
        using (var fault = new AdmissionPoolFault(mode))
            Assert.False(f.Player.AddFlag(Flag.Accelerated, -3, true, 5, uint.MaxValue, 3));
        AssertAdmissionUnchanged(f, before);
    });

    [Theory]
    [InlineData("missing")] [InlineData("null")] [InlineData("small")] [InlineData("disposed")]
    public Task XpPacketPreparationFailureDoesNotDebitOrResetXpAndDoesNotRegister(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture(); NativeAddFlagTestFixture.Ready(f.Owner); f.Rune();
        f.Player.XPBooster = 91; f.Player.XPKiller = 81;
        var before = CaptureAdmission(f);
        using (var fault = new AdmissionPoolFault(mode))
            Assert.False(f.Player.AddFlag(Flag.Superman, 20, true));
        AssertAdmissionUnchanged(f, before);
    });

    [Theory]
    [InlineData("missing")] [InlineData("small")] [InlineData("alias")]
    public Task AddSpellFlagReplacementPreparationFailureRetainsOriginalRealRowAndTimers(string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        Assert.True(f.Player.AddFlag(Flag.Accelerated, 20, true, 3));
        var before = CaptureAdmission(f);
        using (var fault = new AdmissionPoolFault(mode))
            f.Player.AddSpellFlag(Flag.Accelerated, 99, false, 11);
        AssertAdmissionUnchanged(f, before);
    });

    [Fact]
    public Task AddSpellFlagReplacesOnlyItsRowAndPublishesPreparedStatusBeforeTimedFrame()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        Assert.True(f.Player.AddFlag(Flag.Accelerated, 20, true, 3));
        var old = f.Row(Flag.Accelerated); f.Queue.Clear();
        var before = DateTime.Now;
        f.Player.AddSpellFlag(Flag.Accelerated, -7, false, 11);
        var after = DateTime.Now;
        var replacement = f.Row(Flag.Accelerated);
        Assert.NotSame(old, replacement); Assert.Equal(20, old.Secounds); Assert.True(old.RemoveOnDead);
        Assert.Equal(-7, replacement.Secounds); Assert.Equal(11, replacement.InvokerSecouds);
        Assert.False(replacement.RemoveOnDead); Assert.InRange(replacement.Timer, before, after);
        Assert.InRange(replacement.TimerInvoker, before, after);
        var frames = f.Queue.ToArray(); Assert.Equal(2, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Accelerated), frames[0]);
        Assert.Equal(NativeAddFlagTestFixture.Timed(Flag.Accelerated, -7), frames[1]);
    });

    [Theory]
    [InlineData("abcdefgh")] [InlineData("狼wolfMOR")]
    public Task SuccessfulPreparedFramesRetainNativeSealBytesWithoutChangingHeaderLength(string seal)
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture();
        LegacyNativePacket.SealString = seal;
        Assert.True(f.Player.AddFlag(Flag.Deceleration, -3, false, 0, 55, uint.MaxValue));
        byte[] suffix = Encoding.UTF8.GetBytes(seal).Take(8).ToArray();
        var frames = f.Queue.ToArray(); Assert.Equal(2, frames.Length);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.Deceleration).Concat(suffix), frames[0]);
        Assert.Equal(NativeAddFlagTestFixture.Timed(Flag.Deceleration, -3, 55, uint.MaxValue).Concat(suffix), frames[1]);
        Assert.Equal(frames[0].Length - 8, BitConverter.ToUInt16(frames[0], 0));
        Assert.Equal(32, BitConverter.ToUInt16(frames[1], 0));
    });

    [Fact]
    public Task UnserializableShortSealRefusesWithoutErasingPriorPacketBytesOrFlags()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture(); Assert.True(f.Player.AddFlag(Flag.TeamLeader, 30, true));
        var before = CaptureAdmission(f);
        typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, "short");
        Assert.False(f.Player.AddFlag(Flag.Accelerated, 30, true));
        AssertAdmissionUnchanged(f, before);
    });

    [Fact]
    public Task TransportQueueFailureAfterCommitCannotBecomeFalseOrUndoRealFlagAndXp()
        => NativePacketTestThread.Run(() =>
    {
        using var f = new NativeAddFlagTestFixture(); NativeAddFlagTestFixture.Ready(f.Owner); f.Rune();
        var queueField = typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var socket = f.Owner.Socket!; var previous = queueField.GetValue(socket);
        f.Player.XPBooster = 91; f.Player.XPKiller = 81;
        try
        {
            queueField.SetValue(socket, null); // Genuine Send catches its owned queue failure; no accepting sender.
            Assert.True(f.Player.AddFlag(Flag.Superman, 20, true));
            Assert.True(f.Player.BitVector.ContainFlag((int)Flag.Superman));
            Assert.Equal((byte)7, f.Player.XPBooster); Assert.Equal((byte)0, f.Player.XPKiller);
            Assert.Equal(20, f.Row(Flag.Superman).Secounds); Assert.Empty(f.Queue);
        }
        finally { queueField.SetValue(socket, previous); }
    });
}
