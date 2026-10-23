using System.Reflection;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;
using Vector = WolfConquer.Core.World.LegacyNativeStatusFlagsBigVector32;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMonsterAiTests
{
    private static TargetStatusSnapshot CorruptTargetStatus(LegacyNativePlayerRuntime player, string corruption)
    {
        var flags = player.BitVector;
        Assert.True(flags.TryAdd((int)Flag.TeamLeader, 19, true, 3));
        var row = flags.ArrayFlags[(int)Flag.TeamLeader];
        switch (corruption)
        {
            case "vector": player.BitVector = null!; break;
            case "words": flags.bits = null!; break;
            case "dictionary": flags.ArrayFlags = null!; break;
            case "bitmap": flags.Add(7); break;
            case "key": row.Key = 7; break;
            case "deadline": row.Timer = DateTime.MaxValue; break;
            case "alias": Assert.True(flags.ArrayFlags.TryAdd(7, row)); break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        return TargetStatusSnapshot.Capture(player);
    }

    private sealed class TargetStatusQueryProbe(LegacyNativeMonsterRole monster) : IEqualityComparer<int>
    {
        internal bool Armed;
        internal int Reads;
        internal LegacyNativePlayerRuntime? ObservedTarget;
        internal LegacyNativeMobStatus ObservedState;
        public bool Equals(int left, int right) => left == right;
        public int GetHashCode(int value)
        {
            if (Armed)
            {
                Reads++;
                ObservedTarget = monster.Target;
                ObservedState = monster.State;
                throw new InvalidOperationException("Owned status lookup is unavailable.");
            }
            return value.GetHashCode();
        }
    }

    private sealed record TargetStatusRow(Vector.Flag Row, int Key, int Seconds, int Invoker,
        DateTime Timer, DateTime InvokeTimer, bool RemoveOnDeath)
    {
        internal static TargetStatusRow Capture(Vector.Flag row) =>
            new(row, row.Key, row.Secounds, row.InvokerSecouds, row.Timer, row.TimerInvoker, row.RemoveOnDead);
        internal void Unchanged()
        {
            Assert.Equal(Key, Row.Key); Assert.Equal(Seconds, Row.Secounds);
            Assert.Equal(Invoker, Row.InvokerSecouds); Assert.Equal(Timer, Row.Timer);
            Assert.Equal(InvokeTimer, Row.TimerInvoker); Assert.Equal(RemoveOnDeath, Row.RemoveOnDead);
        }
    }

    private sealed record TargetStatusSnapshot(Vector? PhysicalVector, object? Dictionary,
        KeyValuePair<int, Vector.Flag>[]? Entries, uint[]? Words, uint[]? Bytes,
        object? Cache, TargetStatusRow[] Rows)
    {
        private static readonly FieldInfo CacheField = typeof(Vector).GetField("Array", BindingFlags.NonPublic | BindingFlags.Instance)!;
        internal static TargetStatusSnapshot Capture(LegacyNativePlayerRuntime player)
        {
            var vector = player.BitVector;
            if (vector is null) return new(null, null, null, null, null, null, []);
            var entries = vector.ArrayFlags?.ToArray();
            return new(vector, vector.ArrayFlags, entries, vector.bits, vector.bits?.ToArray(),
                CacheField.GetValue(vector), entries?.Select(pair => TargetStatusRow.Capture(pair.Value)).ToArray() ?? []);
        }
        internal void Unchanged(LegacyNativePlayerRuntime player)
        {
            Assert.Same(PhysicalVector, player.BitVector);
            if (PhysicalVector is null) return;
            Assert.Same(Dictionary, PhysicalVector.ArrayFlags); Assert.Equal(Entries, PhysicalVector.ArrayFlags?.ToArray());
            Assert.Same(Words, PhysicalVector.bits); Assert.Equal(Bytes, PhysicalVector.bits);
            Assert.Same(Cache, CacheField.GetValue(PhysicalVector));
            foreach (var row in Rows) row.Unchanged();
        }
    }

    private sealed record TargetMotionSnapshot(ushort X, ushort Y, ushort PX, ushort PY, DateTime Move,
        DateTime Attack, Array Cells, ILegacyNativeMapObj[] Players)
    {
        internal static TargetMotionSnapshot Capture(Scope scope, LegacyNativeMonsterRole role) =>
            new(role.X, role.Y, role.PX, role.PY, role.MoveStamp, role.AttackSpeed, (Array)scope.Map.cells.Clone(),
                scope.Map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Player).ToArray());
        internal void Unchanged(Scope scope, LegacyNativeMonsterRole role)
        {
            Assert.Equal(X, role.X); Assert.Equal(Y, role.Y); Assert.Equal(PX, role.PX); Assert.Equal(PY, role.PY);
            Assert.Equal(Move, role.MoveStamp); Assert.Equal(Attack, role.AttackSpeed);
            Assert.Equal(Cells.Cast<object>(), scope.Map.cells.Cast<object>());
            Assert.Equal(Players, scope.Map.View!.GetAllMapRoles(LegacyNativeMapObjectType.Player));
            Assert.Empty(scope.Queue);
            uint seed = 1;
            Assert.Equal(MsNext(ref seed), scope.Ai.Random.Next());
        }
    }
}
