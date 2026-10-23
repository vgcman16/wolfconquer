using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Effect = WolfConquer.Core.World.LegacyNativeMsgAttackPacket.AttackEffect;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    private static LegacyNativeMagicType.Magic PhysicalAdmissionSpell() => new()
    {
        ID = 16330, Level = 4, Damage = 5, Name = "Owned shared row", Passive = false,
        GDamage = 30010, DamageOnHuman = 30020, Damage2 = 30030,
        Damage3 = 30040, DamageOnMonster = 30050, ColdTime = 800,
    };

    private static object[] PhysicalAdmissionSpellValues(LegacyNativeMagicType.Magic row) =>
        [row.ID, row.Level, row.Name, row.Damage, row.Passive, row.GDamage,
         row.DamageOnHuman, row.Damage2, row.Damage3, row.DamageOnMonster, row.ColdTime];

    private static LegacyNativeMsgSpellAnimation.SpellObj PhysicalAdmissionSentinel() => new(71, 72, Effect.Dash)
    {
        Hit = 73, MoveX = 74, MoveY = 75, x1 = 76, y2 = 77,
        xx1 = 78, yy2 = 79, xxx1 = 80, yyy2 = 81, TailedBeast = 82,
    };

    private static object[] PhysicalAdmissionResultValues(LegacyNativeMsgSpellAnimation.SpellObj row) =>
        [row.UID, row.Damage, row.Hit, row.Effect, row.MoveX, row.MoveY, row.x1,
         row.y2, row.xx1, row.yy2, row.xxx1, row.yyy2, row.TailedBeast];

    private static void CorruptPhysicalAdmission(LegacyNativePlayerRuntime player, string corruption)
    {
        var vector = player.BitVector;
        switch (corruption)
        {
            case "null-vector": player.BitVector = null!; break;
            case "null-words": vector.bits = null!; break;
            case "empty-words": vector.ArrayFlags.Clear(); vector.bits = []; break;
            case "short-words": vector.ArrayFlags.Clear(); vector.bits = new uint[1]; break;
            case "null-registry": vector.ArrayFlags = null!; break;
            case "null-row": vector.ArrayFlags[6] = null!; vector.Add(6); break;
            case "key-mismatch": vector.ArrayFlags[6] = new(7, 30, true, 0); vector.Add(6); break;
            case "row-no-word": vector.ArrayFlags[6] = new(6, 30, true, 0); break;
            case "word-no-row": vector.Add(6); break;
            case "negative-key": vector.ArrayFlags[-1] = new(-1, 30, true, 0); break;
            case "aliased-row":
                var row = new LegacyNativeStatusFlagsBigVector32.Flag(6, 30, true, 0);
                vector.ArrayFlags[6] = row; vector.ArrayFlags[7] = row; vector.Add(6); vector.Add(7);
                break;
            case "deadline-overflow":
                vector.ArrayFlags[6] = new(6, 1, true, 0) { Timer = DateTime.MaxValue }; vector.Add(6);
                break;
            case "invoke-overflow":
                vector.ArrayFlags[6] = new(6, 30, true, 1) { TimerInvoker = DateTime.MaxValue }; vector.Add(6);
                break;
            case "early-stigma-tail":
                Assert.True(vector.TryAdd((int)Flag.Stigma, 30, true, 0));
                vector.ArrayFlags[500] = null!; vector.Add(500);
                break;
            default: throw new InvalidOperationException(corruption);
        }
    }

    private sealed class PhysicalAdmissionVectorSnapshot
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeStatusFlagsBigVector32? vector;
        private readonly uint[]? words;
        private readonly uint[]? copiedWords;
        private readonly ConcurrentDictionary<int, LegacyNativeStatusFlagsBigVector32.Flag>? registry;
        private readonly KeyValuePair<int, LegacyNativeStatusFlagsBigVector32.Flag>[]? rows;
        private readonly object?[][]? rowValues;
        private readonly object? cache;
        private static readonly FieldInfo Cache = typeof(LegacyNativeStatusFlagsBigVector32)
            .GetField("Array", BindingFlags.Instance | BindingFlags.NonPublic)!;

        internal PhysicalAdmissionVectorSnapshot(LegacyNativePlayerRuntime player)
        {
            this.player = player; vector = player.BitVector;
            words = vector?.bits; copiedWords = words?.ToArray();
            registry = vector?.ArrayFlags; rows = registry?.ToArray();
            rowValues = rows?.Select(pair => Values(pair.Value)).ToArray();
            cache = vector is null ? null : Cache.GetValue(vector);
        }

        private static object?[] Values(LegacyNativeStatusFlagsBigVector32.Flag? row) => row is null ? [] :
            [row.Key, row.Secounds, row.InvokerSecouds, row.Timer, row.TimerInvoker, row.RemoveOnDead];

        internal void AssertUnchanged()
        {
            Assert.Same(vector, player.BitVector);
            if (vector is null) return;
            Assert.Same(words, vector.bits); Assert.Equal(copiedWords, vector.bits);
            Assert.Same(registry, vector.ArrayFlags); Assert.Same(cache, Cache.GetValue(vector));
            if (registry is null) return;
            Assert.Equal(rows!.Length, registry.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.Same(rows[i].Value, registry[rows[i].Key]);
                Assert.Equal(rowValues![i], Values(rows[i].Value));
            }
        }
    }

    private sealed class PhysicalAdmissionPrefixSnapshot
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeMonsterRole monster;
        private readonly uint hp, kills, kylin;
        private readonly long rateDraws;
        private readonly byte[][] queued, copiedQueued;
        private readonly LegacyNativeMsgStatus status;
        private readonly uint maxAttack, minAttack;
        private readonly byte[] packet;
        private readonly int size, position;

        internal unsafe PhysicalAdmissionPrefixSnapshot(Scope scope, LegacyNativeMonsterRole monster)
        {
            player = scope.Player; this.monster = monster;
            hp = monster.HitPoints; kills = player.Kills; kylin = player.KylinSigil; rateDraws = player.CompletedRateDraws;
            queued = scope.Queue.ToArray(); copiedQueued = queued.Select(bytes => bytes.ToArray()).ToArray();
            status = scope.Client.Status; maxAttack = status.MaxAttack; minAttack = status.MinAttack;
            packet = new byte[LegacyNativePacket.MAX_SIZE];
            Marshal.Copy((IntPtr)scope.Native.Packet.Memory, packet, 0, packet.Length);
            size = scope.Native.Packet.Size; position = scope.Native.Packet.Position;
        }

        internal unsafe void AssertUnchanged(Scope scope)
        {
            Assert.Equal(hp, monster.HitPoints); Assert.Equal(kills, player.Kills);
            Assert.Equal(kylin, player.KylinSigil); Assert.Equal(rateDraws, player.CompletedRateDraws);
            Assert.Same(status, scope.Client.Status); Assert.Equal(maxAttack, status.MaxAttack); Assert.Equal(minAttack, status.MinAttack);
            var current = scope.Queue.ToArray(); Assert.Equal(queued.Length, current.Length);
            for (int i = 0; i < current.Length; i++)
            {
                Assert.Same(queued[i], current[i]); Assert.Equal(copiedQueued[i], current[i]);
            }
            var memory = new byte[packet.Length];
            Marshal.Copy((IntPtr)scope.Native.Packet.Memory, memory, 0, memory.Length);
            Assert.Equal(packet, memory); Assert.Equal(size, scope.Native.Packet.Size); Assert.Equal(position, scope.Native.Packet.Position);
        }
    }
}
