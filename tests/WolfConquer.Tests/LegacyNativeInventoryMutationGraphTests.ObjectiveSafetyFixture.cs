using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    private sealed record ObjectiveCallerSnapshot(IntPtr Address, int Capacity, int Size, int Position, byte[] Bytes)
    {
        internal static unsafe ObjectiveCallerSnapshot Capture(LegacyNativePacket caller)
        {
            Assert.NotEqual(IntPtr.Zero, (IntPtr)caller.Memory);
            var bytes = new byte[caller.Capacity];
            Marshal.Copy((IntPtr)caller.Memory, bytes, 0, bytes.Length);
            return new((IntPtr)caller.Memory, caller.Capacity, caller.Size, caller.Position, bytes);
        }
        internal unsafe void AssertUnchanged(LegacyNativePacket caller)
        {
            Assert.Equal(Address, (IntPtr)caller.Memory);
            Assert.Equal(Capacity, caller.Capacity);
            Assert.Equal(Size, caller.Size);
            Assert.Equal(Position, caller.Position);
            Assert.Equal(Bytes, Capture(caller).Bytes);
        }
    }

    private sealed class ObjectiveBookSnapshot
    {
        private readonly Dictionary<uint, Row> source, accepted;
        private readonly (uint Key, Row Row, uint Uid, Row.QuestStatus Status, uint Time, uint[] Array, uint[] Values)[] rows;
        private readonly KeyValuePair<uint, Row>[] acceptedRows;
        internal ObjectiveBookSnapshot(LegacyNativeQuestsRuntime quests)
        {
            source = quests.src; accepted = quests.AcceptedQuests;
            rows = source.Select(pair => (pair.Key, pair.Value, pair.Value.UID, pair.Value.Status,
                pair.Value.Time, pair.Value.Intentions, (uint[])pair.Value.Intentions.Clone())).ToArray();
            acceptedRows = accepted.ToArray();
        }
        internal void AssertUnchanged(LegacyNativeQuestsRuntime quests)
        {
            Assert.Same(source, quests.src); Assert.Same(accepted, quests.AcceptedQuests);
            Assert.Equal(rows.Length, source.Count); Assert.Equal(acceptedRows.Length, accepted.Count);
            var actualRows = source.ToArray();
            for (int index = 0; index < rows.Length; index++)
            {
                var before = rows[index]; var actual = actualRows[index];
                Assert.Equal(before.Key, actual.Key); Assert.Same(before.Row, actual.Value);
                Assert.Equal(before.Uid, actual.Value.UID); Assert.Equal(before.Status, actual.Value.Status);
                Assert.Equal(before.Time, actual.Value.Time); Assert.Same(before.Array, actual.Value.Intentions);
                Assert.Equal(before.Values, actual.Value.Intentions);
            }
            var actualAccepted = accepted.ToArray();
            for (int index = 0; index < acceptedRows.Length; index++)
            {
                Assert.Equal(acceptedRows[index].Key, actualAccepted[index].Key);
                Assert.Same(acceptedRows[index].Value, actualAccepted[index].Value);
            }
        }
    }

    private sealed class ObjectiveSealScope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? old = Seal.GetValue(null);
        internal ObjectiveSealScope(bool sealedFrame) => Seal.SetValue(null, sealedFrame ? "TQServer" : null);
        public void Dispose() => Seal.SetValue(null, old);
    }

    private static void ApplyObjectiveOperation(Scope scope, int operation, LegacyNativePacket caller, uint uid, params uint[] values)
    {
        switch (operation)
        {
            case 0: scope.Quests.SetKingDomQuestObjectives(caller, uid, values); break;
            case 1: scope.Quests.SetQuestObjectives(caller, uid, values); break;
            case 2: scope.Quests.IncreaseQuestObjectives(caller, uid, values); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static unsafe void InitializeObjectiveCaller(LegacyNativePacket packet)
    {
        var bytes = Enumerable.Repeat((byte)0xA5, packet.Capacity).ToArray();
        Marshal.Copy(bytes, 0, (IntPtr)packet.Memory, bytes.Length);
        packet.Size = Math.Min(7, packet.Capacity);
        packet.Seek(Math.Min(5, packet.Capacity));
    }

    private static byte[] ObjectiveFrame(uint uid, bool sealedFrame, params uint[] values)
    {
        var frame = DataFrame(uid, values);
        return sealedFrame ? frame.Concat("TQServer"u8.ToArray()).ToArray() : frame;
    }

    private sealed class ObjectiveChangingHash : IEqualityComparer<uint>
    {
        internal int Calls;
        public bool Equals(uint left, uint right) => left == right;
        public int GetHashCode(uint value) => ++Calls;
    }
}
