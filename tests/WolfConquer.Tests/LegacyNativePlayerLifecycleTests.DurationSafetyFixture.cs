using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    private static readonly Flag[] DurationOrder =
    [
        Flag.Cyclone, Flag.Superman, Flag.Oblivion, Flag.FatalStrike, Flag.ShurikenVortex,
        Flag.ChaintBolt, Flag.BlackbeardsRage, Flag.CannonBarrage, Flag.BladeFlurry,
        Flag.SuperCyclone, Flag.DragonCyclone, Flag.Omnipotence, Flag.ManiacDance, Flag.ThunderRampage
    ];

    private sealed record DurationRow(int Key, Flags.Flag Row, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath);

    private sealed record DurationState(Flags? Vector, object? Registry, DurationRow[] Rows,
        uint[]? Words, uint[]? WordValues, object? Cache, ushort Counter, ushort KillCount,
        Flag Used, byte Booster, byte Killer, int PacketSize, int PacketPosition, byte[] PacketBytes)
    {
        private static readonly FieldInfo CacheField = typeof(Flags).GetField("Array", BindingFlags.NonPublic | BindingFlags.Instance)!;

        internal static unsafe DurationState Capture(Scope scope, LegacyNativePlayerRuntime? captured = null)
        {
            var player = captured ?? scope.Player;
            var vector = player.BitVector;
            var rows = vector?.ArrayFlags?.ToArray().Select(pair => new DurationRow(pair.Key, pair.Value,
                pair.Value?.Secounds ?? 0, pair.Value?.InvokerSecouds ?? 0, pair.Value?.Timer ?? default,
                pair.Value?.TimerInvoker ?? default, pair.Value?.RemoveOnDead ?? false)).ToArray() ?? [];
            byte[] packet = new byte[scope.Packet.Capacity];
            Marshal.Copy((IntPtr)scope.Packet.Memory, packet, 0, packet.Length);
            return new(vector, vector?.ArrayFlags, rows, vector?.bits, vector?.bits?.ToArray(),
                vector is null ? null : CacheField.GetValue(vector), player.XPCount, player.KillCounter,
                player.UseXPSpell, player.XPBooster, player.XPKiller, scope.Packet.Size, scope.Packet.Position, packet);
        }

        internal unsafe void Unchanged(Scope scope, LegacyNativePlayerRuntime? captured = null)
        {
            var player = captured ?? scope.Player;
            Assert.Same(Vector, player.BitVector);
            if (Vector is not null)
            {
                Assert.Same(Registry, Vector.ArrayFlags);
                var entries = Vector.ArrayFlags?.ToArray() ?? [];
                Assert.Equal(Rows.Length, entries.Length);
                for (int index = 0; index < Rows.Length; index++)
                {
                    var saved = Rows[index];
                    Assert.Equal(saved.Key, entries[index].Key); Assert.Same(saved.Row, entries[index].Value);
                    if (saved.Row is null) continue;
                    Assert.Equal(saved.Seconds, saved.Row.Secounds); Assert.Equal(saved.Invoker, saved.Row.InvokerSecouds);
                    Assert.Equal(saved.Timer, saved.Row.Timer); Assert.Equal(saved.InvokeTimer, saved.Row.TimerInvoker);
                    Assert.Equal(saved.RemoveOnDeath, saved.Row.RemoveOnDead);
                }
                Assert.Same(Words, Vector.bits); Assert.Equal(WordValues, Vector.bits);
                Assert.Same(Cache, CacheField.GetValue(Vector));
            }
            Assert.Equal(Counter, player.XPCount); Assert.Equal(KillCount, player.KillCounter);
            Assert.Equal(Used, player.UseXPSpell); Assert.Equal(Booster, player.XPBooster); Assert.Equal(Killer, player.XPKiller);
            Assert.Equal(PacketSize, scope.Packet.Size); Assert.Equal(PacketPosition, scope.Packet.Position);
            byte[] bytes = new byte[scope.Packet.Capacity];
            Marshal.Copy((IntPtr)scope.Packet.Memory, bytes, 0, bytes.Length);
            Assert.Equal(PacketBytes, bytes); Assert.Empty(scope.Queue);
        }
    }

    private static void CorruptDuration(LegacyNativePlayerRuntime player, string kind)
    {
        var vector = player.BitVector;
        var row = vector.ArrayFlags[(int)Flag.Cyclone];
        switch (kind)
        {
            case "vector": player.BitVector = null!; break;
            case "words": vector.bits = null!; break;
            case "registry": vector.ArrayFlags = null!; break;
            case "bitmap": vector.Add(7); break;
            case "null-row": vector.ArrayFlags[(int)Flag.Cyclone] = null!; break;
            case "key": row.Key = 7; break;
            case "alias": vector.ArrayFlags.TryAdd(7, row); break;
            case "deadline": row.Timer = DateTime.MaxValue; break;
            case "invoke": row.TimerInvoker = DateTime.MaxValue; row.InvokerSecouds = 3; break;
            case "tail":
                Assert.True(vector.TryAdd((int)Flag.TeamLeader, 9, false, 0));
                vector.ArrayFlags[(int)Flag.TeamLeader].Key = 7;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
