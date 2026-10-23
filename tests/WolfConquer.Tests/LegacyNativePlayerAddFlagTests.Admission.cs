using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerAddFlagTests
{
    private sealed record AdmissionRow(Vector.Flag Row, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool Remove);
    private sealed record AdmissionState(Vector? Vector, object? Dictionary, KeyValuePair<int, Vector.Flag>[]? Entries,
        uint[]? Words, uint[]? Bytes, AdmissionRow[] Rows, byte Booster, byte Killer,
        byte[][] Frames, byte[][] FrameBytes);

    private static AdmissionState CaptureAdmission(NativeAddFlagTestFixture fixture, LegacyNativePlayerRuntime? physicalPlayer = null)
    {
        var player = physicalPlayer ?? fixture.Player;
        var vector = player.BitVector;
        var entries = vector?.ArrayFlags?.ToArray();
        var rows = (entries ?? []).Where(pair => pair.Value is not null).Select(pair => pair.Value)
            .Distinct(ReferenceEqualityComparer.Instance).Cast<Vector.Flag>()
            .Select(row => new AdmissionRow(row, row.Key, row.Secounds, row.InvokerSecouds,
                row.Timer, row.TimerInvoker, row.RemoveOnDead)).ToArray();
        var frames = fixture.Queue.ToArray();
        return new(vector, vector?.ArrayFlags, entries, vector?.bits, vector?.bits?.ToArray(), rows,
            player.XPBooster, player.XPKiller, frames, frames.Select(frame => frame.ToArray()).ToArray());
    }

    private static void AssertAdmissionUnchanged(NativeAddFlagTestFixture fixture, AdmissionState before,
        LegacyNativePlayerRuntime? physicalPlayer = null)
    {
        var player = physicalPlayer ?? fixture.Player;
        Assert.Same(before.Vector, player.BitVector);
        Assert.Same(before.Dictionary, player.BitVector?.ArrayFlags);
        Assert.Same(before.Words, player.BitVector?.bits); Assert.Equal(before.Bytes, player.BitVector?.bits);
        Assert.Equal(before.Entries, player.BitVector?.ArrayFlags?.ToArray());
        foreach (var saved in before.Rows)
        {
            Assert.Equal(saved.Key, saved.Row.Key); Assert.Equal(saved.Seconds, saved.Row.Secounds);
            Assert.Equal(saved.Invoker, saved.Row.InvokerSecouds); Assert.Equal(saved.Timer, saved.Row.Timer);
            Assert.Equal(saved.InvokeTimer, saved.Row.TimerInvoker); Assert.Equal(saved.Remove, saved.Row.RemoveOnDead);
        }
        Assert.Equal(before.Booster, player.XPBooster); Assert.Equal(before.Killer, player.XPKiller);
        var frames = fixture.Queue.ToArray(); Assert.Equal(before.Frames.Length, frames.Length);
        for (int index = 0; index < frames.Length; index++)
        { Assert.Same(before.Frames[index], frames[index]); Assert.Equal(before.FrameBytes[index], frames[index]); }
    }

    [Theory]
    [InlineData("vector")] [InlineData("bitmap")] [InlineData("dictionary")]
    [InlineData("view")] [InlineData("foreign-view")] [InlineData("foreign-player")]
    [InlineData("raw-bit")] [InlineData("raw-registration")] [InlineData("row-key")]
    public Task AdmissionInvalidPhysicalGraphNeverChangesXpOrPublishesARegistration(string malformed)
        => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeAddFlagTestFixture(); NativeAddFlagTestFixture.Ready(fixture.Owner); fixture.Rune();
        Assert.True(fixture.Player.AddFlag(Flag.TeamLeader, 30, true));
        var originalView = fixture.Player.View;
        var originalPlayer = fixture.Owner.Player;
        if (malformed == "vector") fixture.Player.BitVector = null!;
        else if (malformed == "bitmap") fixture.Player.BitVector.bits = null!;
        else if (malformed == "dictionary") fixture.Player.BitVector.ArrayFlags = null!;
        else if (malformed == "view") fixture.Player.View = null!;
        else if (malformed == "foreign-view") fixture.Player.View = new LegacyNativeGameClientRuntime().Player.View;
        else if (malformed == "foreign-player") fixture.Owner.Player = new LegacyNativeGameClientRuntime().Player;
        else if (malformed == "raw-bit") fixture.Player.BitVector.Add(7);
        else if (malformed == "raw-registration") fixture.Player.BitVector.ArrayFlags[7] = new(7, 10, false, 0);
        else fixture.Row(Flag.TeamLeader).Key = 99;
        originalPlayer.XPBooster = 77; originalPlayer.XPKiller = 66;
        try
        {
            // Use original physical receiver even if its client slot was retired.
            var currentPlayer = fixture.Owner.Player;
            var before = CaptureAdmission(fixture, originalPlayer);
            Assert.False(originalPlayer.AddFlag(Flag.Superman, 20, true));
            AssertAdmissionUnchanged(fixture, before, originalPlayer);
            Assert.Same(currentPlayer, fixture.Owner.Player);
            Assert.Equal((byte)77, originalPlayer.XPBooster); Assert.Equal((byte)66, originalPlayer.XPKiller);
            Assert.Single(fixture.Queue);
        }
        finally { fixture.Owner.Player = originalPlayer; originalPlayer.View = originalView; }
    });

    [Fact]
    public Task CorruptPlayerMembershipCannotAuthorizeTheLegacyNegatedPresenceBranch()
        => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeAddFlagTestFixture();
        Assert.True(fixture.Player.BitVector.TryAdd((int)Flag.Dead, 30, false, 0));
        fixture.Player.BitVector.bits[(int)Flag.Dead / 32] = 0;
        bool granted = false;
        Assert.Throws<InvalidDataException>(() =>
        {
            if (!fixture.Player.ContainFlag(Flag.Dead)) granted = true;
        });
        Assert.False(granted); Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task AdmissionDuplicateRejectsBeforeRuneLookupXpResetAndPoolAccess() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeAddFlagTestFixture(); NativeAddFlagTestFixture.Ready(fixture.Owner); fixture.Rune();
        Assert.True(fixture.Player.AddFlag(Flag.Superman, 20, true));
        fixture.Player.XPBooster = 91; fixture.Player.XPKiller = 81;
        fixture.Owner.Rune = null;
        var before = CaptureAdmission(fixture);
        using (var fault = new AdmissionPoolFault("missing"))
            Assert.False(fixture.Player.AddFlag(Flag.Superman, 999, false));
        AssertAdmissionUnchanged(fixture, before);
    });

    [Fact]
    public Task AdmissionConcurrentCompetingAddsCommitExactlyOneRegistrationAndStatusFrame()
        => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeAddFlagTestFixture();
        using var start = new ManualResetEventSlim();
        var first = Task.Run(() => { start.Wait(); return fixture.Player.AddFlag(Flag.TeamLeader, 20, true); });
        var second = Task.Run(() => { start.Wait(); return fixture.Player.AddFlag(Flag.TeamLeader, 30, false); });
        start.Set(); Task.WaitAll(first, second);
        Assert.NotEqual(first.Result, second.Result);
        var row = fixture.Row(Flag.TeamLeader);
        Assert.Equal(first.Result ? 20 : 30, row.Secounds); Assert.Equal(first.Result, row.RemoveOnDead);
        Assert.Equal(NativeAddFlagTestFixture.Status(Flag.TeamLeader), Assert.Single(fixture.Queue));
    });

    private sealed class AdmissionPoolFault : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> owned = new();

        internal AdmissionPoolFault(string mode)
        {
            if (mode == "missing") { Bin.SetValue(null, null); return; }
            if (mode == "null") { Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([null!])); return; }
            int firstCapacity = mode == "small" ? 8 : mode == "exact"
                ? NativeAddFlagTestFixture.Status(Flag.Accelerated).Length : LegacyNativePacket.MAX_SIZE;
            var first = new LegacyNativePacket(firstCapacity); owned.Add(first);
            if (mode == "disposed") first.Dispose();
            if (mode == "alias") { Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([first, first])); return; }
            if (mode is "second-small" or "second-disposed" or "exact")
            {
                int secondCapacity = mode == "second-small" ? 8 : mode == "exact" ? 32 : LegacyNativePacket.MAX_SIZE;
                var second = new LegacyNativePacket(secondCapacity); owned.Add(second);
                if (mode == "second-disposed") second.Dispose();
                Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([first, second])); return;
            }
            Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([first]));
        }

        public void Dispose() { Bin.SetValue(null, prior); foreach (var packet in owned) packet.Dispose(); }
    }
}
