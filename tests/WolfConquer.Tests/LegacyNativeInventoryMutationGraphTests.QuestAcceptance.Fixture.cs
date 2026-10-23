using System.Buffers.Binary;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Quest = WolfConquer.Core.Data.LegacyNativeQuestInfo;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    private static Quest.DBQuest AcceptanceDefinition(ushort uid, uint count) =>
        new() { MissionId = uid, Intentions = count };

    private static byte[] AcceptanceFrame(uint uid, uint time, string? seal = null)
    {
        var bytes = new byte[36 + (seal is null ? 0 : 8)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 36);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2120);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), time);
        if (seal is not null)
            System.Text.Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).CopyTo(bytes.AsSpan(36));
        return bytes;
    }

    private static void AssertAcceptedRow(Row row, uint uid, uint time, int count)
    {
        Assert.Equal(uid, row.UID); Assert.Equal(Status.Accepted, row.Status); Assert.Equal(time, row.Time);
        Assert.Equal(count, row.Intentions.Length); Assert.All(row.Intentions, value => Assert.Equal(0u, value));
    }

    private static void AssertNoPhysicalAcceptedUid(Scope scope, uint uid)
    {
        Assert.DoesNotContain(scope.Quests.src, pair => pair.Key == uid || pair.Value?.UID == uid);
        Assert.DoesNotContain(scope.Quests.AcceptedQuests, pair => pair.Key == uid || pair.Value?.UID == uid);
        Assert.Empty(scope.Queue);
    }

    private sealed class AcceptanceSealScope : IDisposable
    {
        private static readonly FieldInfo Seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? before = Seal.GetValue(null);
        internal AcceptanceSealScope(string? value) => Seal.SetValue(null, value);
        public void Dispose() => Seal.SetValue(null, before);
    }

    private sealed class AcceptanceComparer : IEqualityComparer<uint>
    {
        private readonly bool tens;
        internal int Calls;
        internal uint? ArmedKey;
        internal Action? OnHash;
        internal bool Throw;
        internal AcceptanceComparer(bool tens = false) => this.tens = tens;
        public bool Equals(uint left, uint right) => tens ? left % 10 == right % 10 : left == right;
        public int GetHashCode(uint value)
        {
            Calls++;
            if (ArmedKey == value)
            {
                ArmedKey = null; // One-shot genuine comparer boundary, not a successful provider.
                if (Throw) throw new InvalidOperationException("Owned comparer failure");
                OnHash?.Invoke();
            }
            return (tens ? value % 10 : value).GetHashCode();
        }
    }
}
