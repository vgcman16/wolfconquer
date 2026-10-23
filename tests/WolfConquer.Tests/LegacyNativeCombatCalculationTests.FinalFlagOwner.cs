using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    private sealed class FinalFlagOwnerComparer(LegacyNativePlayerRuntime player,
        LegacyNativeGameClientRuntime client, string drift, int trigger, Queue<byte[]> output) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal int KeySixReads;
        internal bool Fired;
        internal int OutputCountAtDrift;
        internal int PoolCountAtDrift;
        internal LegacyNativePlayerRuntime? ReplacementPlayer;
        internal Vector? ReplacementVector;
        internal LegacyNativeRoleView? ReplacementView;

        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int key)
        {
            if (!Armed || key != 6 || ++KeySixReads != trigger) return key.GetHashCode();
            Armed = false;
            Fired = true;
            OutputCountAtDrift = output.Count;
            PoolCountAtDrift = LegacyNativePacketRecycle.Count;
            switch (drift)
            {
                case "client-player": client.Player = ReplacementPlayer!; break;
                case "vector": player.BitVector = ReplacementVector!; break;
                case "view": player.View = ReplacementView!; break;
                case "uid": player.UID++; break;
                default: throw new ArgumentOutOfRangeException(nameof(drift));
            }
            return key.GetHashCode();
        }
    }

    private sealed record FinalFlagOwnerSnapshot(
        ConcurrentDictionary<int, Vector.Flag> Registry, uint[] Words, uint[] WordValues, object Cache,
        Vector.Flag Row, int Key, int Seconds, int Invoker, DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath,
        bool Attack, DateTime AttackStamp, bool Defense, DateTime DefenseStamp,
        byte Booster, byte Killer, ushort XpCount, ushort KillCount, Flag UsedXp,
        int PacketSize, int PacketPosition, byte[] PacketBytes, byte[][] Frames, byte[][] FrameBytes)
    {
        private static readonly FieldInfo CacheField = typeof(Vector).GetField("Array", BindingFlags.NonPublic | BindingFlags.Instance)!;

        internal static unsafe FinalFlagOwnerSnapshot Capture(Scope scope, Vector vector, LegacyNativePlayerRuntime player)
        {
            var row = vector.ArrayFlags[6];
            byte[] packet = new byte[scope.Native.Packet.Capacity];
            Marshal.Copy((IntPtr)scope.Native.Packet.Memory, packet, 0, packet.Length);
            var frames = scope.Queue.ToArray();
            return new(vector.ArrayFlags, vector.bits, vector.bits.ToArray(), CacheField.GetValue(vector)!,
                row, row.Key, row.Secounds, row.InvokerSecouds, row.Timer, row.TimerInvoker, row.RemoveOnDead,
                player.OnAttackPotion, player.OnAttackPotionStamp, player.OnDefensePotion, player.OnDefensePotionStamp,
                player.XPBooster, player.XPKiller, player.XPCount, player.KillCounter, player.UseXPSpell,
                scope.Native.Packet.Size, scope.Native.Packet.Position, packet, frames,
                frames.Select(frame => frame.ToArray()).ToArray());
        }

        internal unsafe void AssertOperationUnchanged(Scope scope, Vector capturedVector, LegacyNativePlayerRuntime capturedPlayer)
        {
            // A comparer is an external raw writer in these controls. Preserve its
            // intentional binding change, but admit NO flag/potion/XP/output effect.
            Assert.Same(Registry, capturedVector.ArrayFlags);
            var entry = Assert.Single(capturedVector.ArrayFlags);
            Assert.Equal(6, entry.Key); Assert.Same(Row, entry.Value);
            Assert.Same(Words, capturedVector.bits); Assert.Equal(WordValues, capturedVector.bits);
            Assert.Same(Cache, CacheField.GetValue(capturedVector));
            Assert.Equal(Key, Row.Key); Assert.Equal(Seconds, Row.Secounds); Assert.Equal(Invoker, Row.InvokerSecouds);
            Assert.Equal(Timer, Row.Timer); Assert.Equal(InvokeTimer, Row.TimerInvoker); Assert.Equal(RemoveOnDeath, Row.RemoveOnDead);
            Assert.Equal(Attack, capturedPlayer.OnAttackPotion); Assert.Equal(AttackStamp, capturedPlayer.OnAttackPotionStamp);
            Assert.Equal(Defense, capturedPlayer.OnDefensePotion); Assert.Equal(DefenseStamp, capturedPlayer.OnDefensePotionStamp);
            Assert.Equal(Booster, capturedPlayer.XPBooster); Assert.Equal(Killer, capturedPlayer.XPKiller);
            Assert.Equal(XpCount, capturedPlayer.XPCount); Assert.Equal(KillCount, capturedPlayer.KillCounter);
            Assert.Equal(UsedXp, capturedPlayer.UseXPSpell);
            Assert.Equal(PacketSize, scope.Native.Packet.Size); Assert.Equal(PacketPosition, scope.Native.Packet.Position);
            byte[] packet = new byte[scope.Native.Packet.Capacity];
            Marshal.Copy((IntPtr)scope.Native.Packet.Memory, packet, 0, packet.Length);
            Assert.Equal(PacketBytes, packet);
            var afterFrames = scope.Queue.ToArray(); Assert.Equal(Frames.Length, afterFrames.Length);
            for (int index = 0; index < Frames.Length; index++)
            {
                Assert.Same(Frames[index], afterFrames[index]); Assert.Equal(FrameBytes[index], afterFrames[index]);
            }
        }
    }

    [Theory]
    [InlineData("flag", "client-player")] [InlineData("flag", "vector")]
    [InlineData("flag", "view")] [InlineData("flag", "uid")]
    [InlineData("attack", "client-player")] [InlineData("attack", "vector")]
    [InlineData("attack", "view")] [InlineData("attack", "uid")]
    [InlineData("defense", "client-player")] [InlineData("defense", "vector")]
    [InlineData("defense", "view")] [InlineData("defense", "uid")]
    public Task FinalCommitComparerOwnerDriftCannotPublishFlagPotionOrXpPrefix(string operation, string drift)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var client = scope.Client; var player = scope.Player; var vector = player.BitVector; var view = player.View;
        uint uid = player.UID;
        Assert.Equal(6, (int)Flag.TeamLeader);
        Assert.True(vector.TryAdd(6, 100, true, 3));
        _ = vector.GetFlags(); // Real cache preimage, not fabricated membership.
        client.Rune = null; // Optional XP metadata is unrelated to this ownership proof.
        player.XPBooster = 73; player.XPKiller = 91; player.XPCount = 19;
        player.KillCounter = 123; player.UseXPSpell = Flag.Superman;
        player.OnAttackPotionStamp = new DateTime(2001, 2, 3);
        player.OnDefensePotionStamp = new DateTime(2004, 5, 6);
        player.SendString(scope.Native.Packet, LegacyNativeMsgStringPacket.StringID.Effect, false, "prior-flag");

        // Both replacements are genuine physical owners. Same UID on a new Player
        // must not make it the captured owner; a new View keeps the same real client.
        var replacement = new LegacyNativePlayerRuntime(client) { UID = uid };
        var comparer = new FinalFlagOwnerComparer(player, client, drift,
            operation == "flag" ? 3 : 4, scope.Queue)
        {
            ReplacementPlayer = replacement,
            ReplacementVector = replacement.BitVector,
            ReplacementView = new LegacyNativeRoleView(client)
        };
        vector.ArrayFlags = new ConcurrentDictionary<int, Vector.Flag>(vector.ArrayFlags, comparer);
        var before = FinalFlagOwnerSnapshot.Capture(scope, vector, player);
        int poolBefore = LegacyNativePacketRecycle.Count;
        comparer.Armed = true;
        if (operation == "flag") Assert.False(player.AddFlag(Flag.Cyclone, 60, true));
        else if (operation == "attack") player.ActiveAttackPotion(7);
        else player.ActiveDefensePotion(7);

        Assert.True(comparer.Fired);
        Assert.Equal(operation == "flag" ? 3 : 4, comparer.KeySixReads);
        Assert.Equal(1, comparer.OutputCountAtDrift);
        Assert.Equal(poolBefore - (operation == "flag" ? 1 : 2), comparer.PoolCountAtDrift);
        before.AssertOperationUnchanged(scope, vector, player);
        Assert.Same(drift == "client-player" ? replacement : player, client.Player);
        Assert.Same(drift == "vector" ? replacement.BitVector : vector, player.BitVector);
        Assert.Same(drift == "view" ? comparer.ReplacementView : view, player.View);
        Assert.Equal(drift == "uid" ? uid + 1 : uid, player.UID);
        Assert.Empty(replacement.BitVector.ArrayFlags);
    });
}
