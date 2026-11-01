using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMonsterPhysicalRangeMetadata
{
    private sealed class FamilyRows
    {
        internal readonly IReadOnlyList<Monster> Source;
        internal readonly Monster[] Values;
        private readonly Monster[]? array;
        private readonly List<Monster>? list;
        internal FamilyRows(IReadOnlyList<Monster> source)
        {
            Source = source ?? throw Invalid();
            if (source.GetType() == typeof(Monster[])) array = (Monster[])source;
            else if (source.GetType() == typeof(List<Monster>)) list = (List<Monster>)source;
            else throw Invalid();
            int count = array?.Length ?? list!.Count;
            Values = new Monster[count];
            for (int i = 0; i < count; i++) Values[i] = (array is not null ? array[i] : list![i]) ?? throw Invalid();
        }
        internal bool IsCurrentPlain()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (int i = 0; i < Values.Length; i++)
                if (!ReferenceEquals(Values[i], array is not null ? array[i] : list![i])) return false;
            return true;
        }
    }
}
