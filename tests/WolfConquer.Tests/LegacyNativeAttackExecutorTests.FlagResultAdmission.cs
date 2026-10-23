using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    [Theory]
    [InlineData(true, "missing")]
    [InlineData(true, "null")]
    [InlineData(true, "small")]
    [InlineData(true, "disposed")]
    [InlineData(false, "missing")]
    [InlineData(false, "null")]
    [InlineData(false, "small")]
    [InlineData(false, "disposed")]
    public Task RefusedKnownAbsentWeaponFlagDoesNotPublishAClaimedSuccessAnimation(bool stigma, string poolFault)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.True(s.Player.AddFlag(Flag.TeamLeader, 31, true, 3));
        var retained = s.Player.BitVector.ArrayFlags[(int)Flag.TeamLeader];
        PrepareEffectResultCallerPacket(s);
        var before = CaptureEffectResultState(s, retained);
        using (var fault = new EffectResultPoolFault(poolFault)) ExecuteEffectResult(stigma, s);
        AssertEffectResultUnchanged(s, retained, before);
        Assert.False(s.Player.ContainFlag(stigma ? Flag.Stigma : Flag.MagicShield));
    });

    [Theory]
    [InlineData(true, "dictionary")]
    [InlineData(true, "words")]
    [InlineData(true, "bit-only")]
    [InlineData(true, "row-key")]
    [InlineData(false, "dictionary")]
    [InlineData(false, "words")]
    [InlineData(false, "bit-only")]
    [InlineData(false, "row-key")]
    public Task UnavailableWeaponFlagMembershipPropagatesBeforeGrantOrCallerPacketPublication(bool stigma, string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.True(s.Player.AddFlag(Flag.TeamLeader, 31, true, 3));
        var vector = s.Player.BitVector;
        var retained = vector.ArrayFlags[(int)Flag.TeamLeader];
        if (malformed == "dictionary") vector.ArrayFlags = null!;
        else if (malformed == "words") vector.bits = null!;
        else if (malformed == "bit-only") vector.Add((int)(stigma ? Flag.Stigma : Flag.MagicShield));
        else retained.Key = (int)(stigma ? Flag.Stigma : Flag.MagicShield);
        PrepareEffectResultCallerPacket(s);
        var before = CaptureEffectResultState(s, retained);
        Assert.Throws<InvalidDataException>(() => ExecuteEffectResult(stigma, s));
        AssertEffectResultUnchanged(s, retained, before);
    });

    private static void ExecuteEffectResult(bool stigma, Scope scope)
    {
        if (stigma) LegacyNativeEffectStigma.Execute(null, scope.Client, scope.Packet, null);
        else LegacyNativeEffectShield.Execute(null, scope.Client, scope.Packet, null);
    }

    private sealed record EffectResultState(
        LegacyNativePlayerRuntime Player, Vector Vector,
        object? Registrations, KeyValuePair<int, Vector.Flag>[]? Entries, uint[]? Words, uint[]? WordValues,
        int Key, int Seconds, int Invoker, DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath,
        byte[] CallerBytes, int CallerSize, int CallerPosition, byte[][] Frames, byte[][] FrameBytes);

    private static unsafe void PrepareEffectResultCallerPacket(Scope scope)
    {
        var sentinel = new byte[scope.Packet.Capacity];
        Array.Fill(sentinel, (byte)0xCD);
        Marshal.Copy(sentinel, 0, (IntPtr)scope.Packet.Memory, sentinel.Length);
        scope.Packet.Size = 32;
        scope.Packet.Seek(17);
    }

    private static EffectResultState CaptureEffectResultState(Scope scope, Vector.Flag retained)
    {
        var vector = scope.Player.BitVector;
        var frames = scope.Queue.ToArray();
        return new(scope.Player, vector, vector.ArrayFlags, vector.ArrayFlags?.ToArray(), vector.bits, vector.bits?.ToArray(),
            retained.Key, retained.Secounds, retained.InvokerSecouds, retained.Timer, retained.TimerInvoker,
            retained.RemoveOnDead, CopyEffectResultPacket(scope.Packet), scope.Packet.Size, scope.Packet.Position,
            frames, frames.Select(frame => frame.ToArray()).ToArray());
    }

    private static void AssertEffectResultUnchanged(Scope scope, Vector.Flag retained, EffectResultState before)
    {
        var vector = scope.Player.BitVector;
        Assert.Same(before.Player, scope.Player);
        Assert.Same(before.Vector, vector);
        Assert.Same(before.Registrations, vector.ArrayFlags);
        Assert.Equal(before.Entries, vector.ArrayFlags?.ToArray());
        Assert.Same(before.Words, vector.bits);
        Assert.Equal(before.WordValues, vector.bits);
        Assert.Equal(before.Key, retained.Key);
        Assert.Equal(before.Seconds, retained.Secounds);
        Assert.Equal(before.Invoker, retained.InvokerSecouds);
        Assert.Equal(before.Timer, retained.Timer);
        Assert.Equal(before.InvokeTimer, retained.TimerInvoker);
        Assert.Equal(before.RemoveOnDeath, retained.RemoveOnDead);
        Assert.Equal(before.CallerBytes, CopyEffectResultPacket(scope.Packet));
        Assert.Equal(before.CallerSize, scope.Packet.Size);
        Assert.Equal(before.CallerPosition, scope.Packet.Position);
        var frames = scope.Queue.ToArray();
        Assert.Equal(before.Frames.Length, frames.Length);
        for (int index = 0; index < frames.Length; index++)
        {
            Assert.Same(before.Frames[index], frames[index]);
            Assert.Equal(before.FrameBytes[index], frames[index]);
        }
    }

    private static unsafe byte[] CopyEffectResultPacket(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((IntPtr)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }

    private sealed class EffectResultPoolFault : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Bin.GetValue(null);
        private readonly LegacyNativePacket? owned;

        internal EffectResultPoolFault(string mode)
        {
            if (mode == "missing") { Bin.SetValue(null, null); return; }
            if (mode == "null")
            {
                Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
                return;
            }
            owned = new LegacyNativePacket(mode == "small" ? 1 : LegacyNativePacket.MAX_SIZE);
            if (mode == "disposed") owned.Dispose();
            Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { owned }));
        }

        public void Dispose()
        {
            Bin.SetValue(null, previous);
            owned?.Dispose();
        }
    }
}
