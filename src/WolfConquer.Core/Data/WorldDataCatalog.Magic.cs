namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    public IReadOnlyDictionary<int, MagicType> MagicById { get; }

    public IReadOnlyDictionary<long, IReadOnlyList<MagicEffect>> MagicEffectsById { get; }

    public IReadOnlyDictionary<(long Id, int Occurrence), MagicEffect> MagicEffectByIdAndOccurrence { get; }

    public IReadOnlyDictionary<int, MagicTypeOperationEntry> MagicTypeOperationsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MagicTypeOperationEntry>> MagicTypeOperationsByMode { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MagicTypeOperationEntry>> MagicTypeOperationsByReferencedMagicTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MagicType>> MagicByTypeId { get; }

    private static IReadOnlyDictionary<int, IReadOnlyList<MagicTypeOperationEntry>> BuildMagicTypeOperationReferenceIndex(
        IReadOnlyList<MagicTypeOperationEntry> values)
    {
        return values
            .SelectMany(entry => entry.ReferencedMagicTypeIds.Distinct().Select(magicTypeId => (magicTypeId, entry)))
            .GroupBy(pair => pair.magicTypeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MagicTypeOperationEntry>)group.Select(pair => pair.entry).ToArray());
    }
}
