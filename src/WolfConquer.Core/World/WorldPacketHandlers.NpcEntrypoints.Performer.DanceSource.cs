using System.Collections;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacyPerformerSpellRows : IReadOnlyList<PlayerSpellSnapshotEntry>
    {
        internal readonly PlayerSpellSnapshotEntry[] Values;
        internal LegacyPerformerSpellRows(PlayerSpellSnapshotEntry[] values) => Values = values;
        public int Count => Values.Length;
        public PlayerSpellSnapshotEntry this[int index] => Values[index];
        public IEnumerator<PlayerSpellSnapshotEntry> GetEnumerator() =>
            ((IEnumerable<PlayerSpellSnapshotEntry>)Values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class LegacyPerformerSpellSource
    {
        private readonly PlayerSpellSnapshotEntry[]? array;
        private readonly List<PlayerSpellSnapshotEntry>? list;
        internal readonly PlayerSpellSnapshotEntry[] Rows;

        internal LegacyPerformerSpellSource(IReadOnlyList<PlayerSpellSnapshotEntry> source)
        {
            switch (source)
            {
                case PlayerSpellSnapshotEntry[] arrayRows: array = arrayRows; break;
                case List<PlayerSpellSnapshotEntry> listRows when listRows.GetType() == typeof(List<PlayerSpellSnapshotEntry>):
                    list = listRows; break;
                case LegacyPerformerSpellRows frozenRows: array = frozenRows.Values; break;
                default: throw InvalidPerformerDanceState();
            }
            var count = Count;
            if (count > ushort.MaxValue) throw InvalidPerformerDanceState();
            Rows = new PlayerSpellSnapshotEntry[count];
            var ids = new HashSet<ushort>();
            for (var index = 0; index < count; index++)
            {
                var row = Read(index);
                if (row is null || row.Id == 0 || row.Experience < 0 || !ids.Add(row.Id))
                    throw InvalidPerformerDanceState();
                Rows[index] = row;
            }
            if (!IsCurrent()) throw InvalidPerformerDanceState();
        }

        private int Count => array is not null ? array.Length : list!.Count;
        private PlayerSpellSnapshotEntry Read(int index) => array is not null ? array[index] : list![index];

        // Only genuine array/List backing or this producer's private frozen rows
        // reach this check. There is no caller indexer, enumerator or comparer.
        internal bool IsCurrent()
        {
            if (Count != Rows.Length) return false;
            for (var index = 0; index < Rows.Length; index++)
                if (!ReferenceEquals(Read(index), Rows[index])) return false;
            return true;
        }
    }
}
