using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    public static IEnumerable<object[]> SelfStatusLoanFailures()
    {
        foreach (bool stigma in new[] { false, true })
        foreach (bool existing in new[] { false, true })
        {
            foreach (string fault in new[] { "missing", "null-first", "null-animation", "short-animation", "disposed-animation", "caller-first" })
                yield return [stigma, existing, fault];
            if (!existing)
                foreach (string fault in new[] { "short-status", "alias", "caller-animation" })
                    yield return [stigma, existing, fault];
        }
    }

    [Theory]
    [MemberData(nameof(SelfStatusLoanFailures))]
    public Task SelfStatusLoanRefusalPreservesWholeGraphXpPriorQueueAndCallerPreimage(bool stigma, bool existing, string fault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, existing);
        var before = SelfStatusBefore.Capture(s, retained);
        int animationLength = SelfStatusAnimation(stigma, existing).Length;
        int statusLength = NativeAddFlagTestFixture.Status(SelfStatusFlag(stigma)).Length;
        using (var pool = new SelfStatusPoolScope(fault, existing, s.Packet, animationLength, statusLength))
            Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        before.Unchanged(s, retained);
        Assert.Equal(existing, s.Player.ContainFlag(SelfStatusFlag(stigma)));
    });

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public Task MissingRealAnimationPoolRefusesBeforeEitherPresentOrNewFlagOutput(bool stigma, bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, existing);
        var before = SelfStatusBefore.Capture(s, retained);
        LegacyNativePoolClients.GamePoll = null;
        Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        before.Unchanged(s, retained);
    });

    [Theory]
    [InlineData(false, false, false)] [InlineData(false, false, true)]
    [InlineData(false, true, false)] [InlineData(false, true, true)]
    [InlineData(true, false, false)] [InlineData(true, false, true)]
    [InlineData(true, true, false)] [InlineData(true, true, true)]
    public unsafe Task ShortOrDisposedCallerCannotCommitAFlagOrBorrowAnotherPacket(bool stigma, bool existing, bool disposed)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, existing);
        var before = SelfStatusBefore.Capture(s, retained);
        using var caller = new LegacyNativePacket(SelfStatusAnimation(stigma, existing).Length - 1);
        var bytes = Enumerable.Repeat((byte)0xB7, caller.Capacity).ToArray();
        Marshal.Copy(bytes, 0, (IntPtr)caller.Memory, bytes.Length); caller.Size = caller.Capacity; caller.Seek(7);
        if (disposed) caller.Dispose();
        var address = (IntPtr)caller.Memory; int size = caller.Size, position = caller.Position;
        Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, caller, stigma));
        before.Unchanged(s, retained); Assert.Equal(address, (IntPtr)caller.Memory);
        Assert.Equal(size, caller.Size); Assert.Equal(position, caller.Position);
        if (!disposed)
        {
            var actual = new byte[caller.Capacity]; Marshal.Copy((IntPtr)caller.Memory, actual, 0, actual.Length);
            Assert.Equal(bytes, actual);
        }
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task InvalidCapturedSealCannotCommitOrDirtyTheCaller(bool stigma) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, false);
        var before = SelfStatusBefore.Capture(s, retained);
        typeof(LegacyNativePacket).GetField("seal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .SetValue(null, "bad");
        Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        before.Unchanged(s, retained);
    });
}
