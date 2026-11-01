using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

/// <summary>One raw data row. Field indexes carry no inferred gameplay meaning.</summary>
public sealed class Chinese7937CombatGearCatalogRow
{
    private readonly ReadOnlyCollection<uint> values;
    private readonly ReadOnlyCollection<string> rawFields;

    internal Chinese7937CombatGearCatalogRow(uint[] values, string[] rawFields)
    {
        this.values = Array.AsReadOnly(values);
        this.rawFields = Array.AsReadOnly(rawFields);
    }

    public IReadOnlyList<uint> Values => values;
    public IReadOnlyList<string> RawFields => rawFields;
}
