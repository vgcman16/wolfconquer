using System.Collections.Concurrent;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    public static IEnumerable<object[]> SelfStatusFinalDrifts()
    {
        foreach (bool stigma in new[] { false, true })
        foreach (string mode in new[] { "uid", "player", "vector", "view", "map", "pool", "words", "caller-dispose", "loan-dispose",
            "animation-size", "animation-cursor", "animation-byte", "status-size", "status-cursor", "status-byte",
            "caller-size", "caller-cursor", "caller-byte" })
            yield return [stigma, mode];
    }

    [Theory]
    [MemberData(nameof(SelfStatusFinalDrifts))]
    public unsafe Task LastRealComparerCannotCommitPreparedEffectToChangedOwnersOrDisposedStorage(bool stigma, string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, false);
        var player = s.Player; var vector = player.BitVector;
        int animationLength = SelfStatusAnimation(stigma, false).Length;
        int statusLength = NativeAddFlagTestFixture.Status(SelfStatusFlag(stigma)).Length;
        using var pool = new SelfStatusPoolScope("exact", false, s.Packet, animationLength, statusLength);
        var comparer = new SelfStatusFinalComparer(s.Client, player, vector, s.Packet, pool.Status!, pool.Animation!, mode, animationLength);
        vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(vector.ArrayFlags, comparer);
        var registry = vector.ArrayFlags; var words = vector.bits; var oldWords = words.ToArray();
        object? cache = SelfStatusCache.GetValue(vector); var before = CaptureEffectResultState(s, retained);
        comparer.Armed = true;
        Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        Assert.True(comparer.Triggered); Assert.Same(registry, vector.ArrayFlags); Assert.Same(words, vector.bits);
        Assert.Same(cache, SelfStatusCache.GetValue(vector));
        Assert.False(registry.ContainsKey((int)SelfStatusFlag(stigma)));
        Assert.Same(retained, registry[(int)Flag.TeamLeader]); Assert.Equal(31, retained.Secounds);
        if (mode != "words") Assert.Equal(oldWords, vector.bits);
        var frames = s.Queue.ToArray(); Assert.Equal(before.Frames.Length, frames.Length);
        for (int i = 0; i < frames.Length; i++) { Assert.Same(before.Frames[i], frames[i]); Assert.Equal(before.FrameBytes[i], frames[i]); }
        if (mode == "caller-dispose") Assert.True(s.Packet.Memory == null);
        else
        {
            var expectedBytes = before.CallerBytes.ToArray();
            if (mode == "caller-byte") expectedBytes[0] ^= 1;
            Assert.Equal(expectedBytes, CopyEffectResultPacket(s.Packet));
            Assert.Equal(mode == "caller-size" ? 1032 : before.CallerSize, s.Packet.Size);
            Assert.Equal(mode == "caller-cursor" ? s.Packet.Capacity + 1 : before.CallerPosition, s.Packet.Position);
        }
        // The external comparer writes are deliberately NOT rolled back. Only
        // the rejected effect's flag/XP/queue/caller contributions are denied.
        Assert.Equal((byte)5, player.XPBooster); Assert.Equal((byte)6, player.XPKiller);
    });

    private sealed class SelfStatusFinalComparer(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector,
        LegacyNativePacket caller, LegacyNativePacket status, LegacyNativePacket animation, string mode, int length) : IEqualityComparer<int>
    {
        internal bool Armed, Triggered;
        public bool Equals(int left, int right) => left == right;
        public unsafe int GetHashCode(int value)
        {
            if (Armed && animation.Size == length)
            {
                Assert.True(Monitor.IsEntered(client.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(player.SpellAdmissionSyncRoot));
                Assert.True(Monitor.IsEntered(vector.SyncRoot));
                Armed = false; Triggered = true;
                switch (mode)
                {
                    case "uid": player.UID++; break;
                    case "player": client.Player = new LegacyNativePlayerRuntime(client) { UID = player.UID }; break;
                    case "vector": player.BitVector = new LegacyNativeStatusFlagsBigVector32(576); break;
                    case "view": player.View = new LegacyNativeRoleView(client); break;
                    case "map": client.Map = new LegacyNativeGameMap(128, 128, 1002) { View = new LegacyNativeMapView(128, 128) }; break;
                    case "pool": LegacyNativePoolClients.GamePoll = new(); break;
                    case "words": vector.Add(500); break;
                    case "caller-dispose": caller.Dispose(); break;
                    case "loan-dispose": animation.Dispose(); break;
                    case "animation-size": animation.Size = 1032; break;
                    case "animation-cursor": animation.Seek(animation.Capacity + 1); break;
                    case "animation-byte": animation.Memory[0] ^= 1; break;
                    case "status-size": status.Size = 1032; break;
                    case "status-cursor": status.Seek(status.Capacity + 1); break;
                    case "status-byte": status.Memory[0] ^= 1; break;
                    case "caller-size": caller.Size = 1032; break;
                    case "caller-cursor": caller.Seek(caller.Capacity + 1); break;
                    case "caller-byte": caller.Memory[0] ^= 1; break;
                    default: throw new InvalidOperationException(mode);
                }
            }
            return value.GetHashCode();
        }
    }

    [Theory]
    [InlineData(false, "uid")] [InlineData(true, "uid")]
    [InlineData(false, "player")] [InlineData(true, "player")]
    [InlineData(false, "vector")] [InlineData(true, "vector")]
    public Task FirstMembershipComparerCannotRedefineTheCapturedInvocationOwner(bool stigma, string mode)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, stigma, false);
        var player = s.Player; var vector = player.BitVector;
        using var pool = new SelfStatusPoolScope("exact", false, s.Packet,
            SelfStatusAnimation(stigma, false).Length, NativeAddFlagTestFixture.Status(SelfStatusFlag(stigma)).Length);
        // A fresh real loan has Size0. Trigger at the FIRST query, before any
        // preparation could otherwise recapture a changed UID as new authority.
        var comparer = new SelfStatusFinalComparer(s.Client, player, vector, s.Packet, pool.Status!, pool.Animation!, mode, 0);
        vector.ArrayFlags = new ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>(vector.ArrayFlags, comparer);
        var registry = vector.ArrayFlags; var words = vector.bits; var copiedWords = words.ToArray();
        var before = CaptureEffectResultState(s, retained); comparer.Armed = true;
        Assert.False(LegacyNativeSelfStatusEffect.TryExecute(s.Client, s.Packet, stigma));
        Assert.True(comparer.Triggered); Assert.Same(registry, vector.ArrayFlags);
        Assert.Same(words, vector.bits); Assert.Equal(copiedWords, vector.bits);
        Assert.False(registry.ContainsKey((int)SelfStatusFlag(stigma)));
        Assert.Equal(before.CallerBytes, CopyEffectResultPacket(s.Packet));
        Assert.Equal(before.CallerSize, s.Packet.Size); Assert.Equal(before.CallerPosition, s.Packet.Position);
        Assert.Equal(before.FrameBytes, s.Queue.ToArray()); Assert.Equal(0, pool.Animation!.Size);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RejectedSelfStatusTokensAreOpaqueAndConsumedForNewAndPresentFlags(bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, true, existing);
        var vector = s.Player.BitVector; var before = SelfStatusBefore.Capture(s, retained);
        lock (s.Client.SpellAdmissionSyncRoot)
        lock (s.Player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSelfStatusBindings(s.Client, s.Player, vector, s.Player.View, s.Packet);
            Assert.True(vector.TryPrepareSelfStatus((int)Flag.Stigma, 90, out var token, out bool present, out var words));
            using var publication = LegacyNativePreparedSelfStatusPublication.Create(bindings, token!, true, present, words);
            Assert.False(vector.TryAcceptSelfStatus(new LegacyNativeStatusFlagsBigVector32.SelfStatusChange(), bindings, publication));
            s.Player.UID = 18;
            Assert.False(vector.TryAcceptSelfStatus(token!, bindings, publication));
            s.Player.UID = 17;
            Assert.False(vector.TryAcceptSelfStatus(token!, bindings, publication));
        }
        before.Unchanged(s, retained);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task AnotherOpaqueTokenCannotSupplyPublicationForThisStatusPlan(bool existing)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, true, existing);
        var vector = s.Player.BitVector; var before = SelfStatusBefore.Capture(s, retained);
        lock (s.Client.SpellAdmissionSyncRoot)
        lock (s.Player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSelfStatusBindings(s.Client, s.Player, vector, s.Player.View, s.Packet);
            Assert.True(vector.TryPrepareSelfStatus((int)Flag.Stigma, 90, out var token, out bool present, out var words));
            using var publication = LegacyNativePreparedSelfStatusPublication.Create(bindings,
                new LegacyNativeStatusFlagsBigVector32.SelfStatusChange(), true, present, words);
            Assert.False(vector.TryAcceptSelfStatus(token!, bindings, publication));
        }
        before.Unchanged(s, retained);
    });

    [Fact]
    public Task MutatingCopiedWordsCannotPairAMismatchedStatusFrameWithTheGenuineCommit()
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var retained = PrepareSelfStatusCase(s, true, false);
        var vector = s.Player.BitVector; var before = SelfStatusBefore.Capture(s, retained);
        lock (s.Client.SpellAdmissionSyncRoot)
        lock (s.Player.SpellAdmissionSyncRoot)
        lock (vector.SyncRoot)
        {
            var bindings = new LegacyNativeSelfStatusBindings(s.Client, s.Player, vector, s.Player.View, s.Packet);
            Assert.True(vector.TryPrepareSelfStatus((int)Flag.Stigma, 90, out var token, out bool present, out var words));
            words![0] ^= 1;
            using var publication = LegacyNativePreparedSelfStatusPublication.Create(bindings, token!, true, present, words);
            Assert.False(vector.TryAcceptSelfStatus(token!, bindings, publication));
        }
        before.Unchanged(s, retained);
    });
}
