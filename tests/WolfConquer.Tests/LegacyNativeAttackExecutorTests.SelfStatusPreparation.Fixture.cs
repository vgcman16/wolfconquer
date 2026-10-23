using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackExecutorTests
{
    private static readonly FieldInfo SelfStatusCache = typeof(LegacyNativeStatusFlagsBigVector32)
        .GetField("Array", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static byte[] SelfStatusAnimation(bool stigma, bool existing) => LegacyNativeSpellAnimationTests.Frame(
        LegacyNativeSpellAnimationTests.Payload(17, 0, 32, 42, stigma ? (ushort)1095 : (ushort)1020,
            4, 0, 0, 0, existing ? [] : [LegacyNativeSpellAnimationTests.Target(17, 0, true, 0)]));

    private static Flag SelfStatusFlag(bool stigma) => stigma ? Flag.Stigma : Flag.MagicShield;

    private sealed record SelfStatusBefore(EffectResultState State, object? Cache, byte Booster, byte Killer,
        ushort Counter, ushort Stamina, ushort Mana)
    {
        internal static SelfStatusBefore Capture(Scope scope, LegacyNativeStatusFlagsBigVector32.Flag retained) =>
            new(CaptureEffectResultState(scope, retained), SelfStatusCache.GetValue(scope.Player.BitVector),
                scope.Player.XPBooster, scope.Player.XPKiller, scope.Player.XPCount, scope.Player.Stamina, scope.Player.Mana);

        internal void Unchanged(Scope scope, LegacyNativeStatusFlagsBigVector32.Flag retained)
        {
            AssertEffectResultUnchanged(scope, retained, State);
            Assert.Same(Cache, SelfStatusCache.GetValue(scope.Player.BitVector));
            Assert.Equal(Booster, scope.Player.XPBooster); Assert.Equal(Killer, scope.Player.XPKiller);
            Assert.Equal(Counter, scope.Player.XPCount); Assert.Equal(Stamina, scope.Player.Stamina); Assert.Equal(Mana, scope.Player.Mana);
        }
    }

    private static LegacyNativeStatusFlagsBigVector32.Flag PrepareSelfStatusCase(Scope scope, bool stigma, bool existing)
    {
        Assert.True(scope.Player.AddFlag(Flag.TeamLeader, 31, true, 3));
        if (existing) Assert.True(scope.Player.BitVector.TryAdd((int)SelfStatusFlag(stigma), 9, false, 0));
        scope.Player.XPBooster = 5; scope.Player.XPKiller = 6;
        PrepareEffectResultCallerPacket(scope);
        return scope.Player.BitVector.ArrayFlags[(int)Flag.TeamLeader];
    }

    private sealed class SelfStatusPoolScope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previous = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> owned = new();
        internal LegacyNativePacket? Status { get; }
        internal LegacyNativePacket? Animation { get; }

        internal SelfStatusPoolScope(string mode, bool existing, LegacyNativePacket caller, int animationLength, int statusLength)
        {
            if (mode == "missing") { Bin.SetValue(null, null); return; }
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            if (!existing)
            {
                Status = mode switch
                {
                    "null-first" => null,
                    "caller-first" => caller,
                    "short-status" => Packet(statusLength - 1),
                    _ => Packet(statusLength),
                };
                queue.Enqueue(Status!);
            }
            Animation = mode switch
            {
                "null-first" when existing => null,
                "null-animation" => null,
                "caller-first" when existing => caller,
                "caller-animation" => caller,
                "alias" => Status,
                "short-animation" => Packet(animationLength - 1),
                _ => Packet(animationLength),
            };
            if (mode == "disposed-animation") Animation!.Dispose();
            queue.Enqueue(Animation!); Bin.SetValue(null, queue);
        }

        private LegacyNativePacket Packet(int capacity)
        {
            var packet = new LegacyNativePacket(capacity); owned.Add(packet); return packet;
        }

        public void Dispose()
        {
            Bin.SetValue(null, previous);
            foreach (var packet in owned) packet.Dispose();
        }
    }
}
