using System.Collections.Frozen;

namespace WolfConquer.Core.Client;

/// <summary>Immutable7937 catalog owner for exact identity lookup and Echo request data preparation.</summary>
/// <remarks>No player inventory, currency, activation, persistence or default-profile ownership is implied.</remarks>
public sealed partial class Chinese7937EchoRuntimeCatalog
{
    private readonly FrozenDictionary<uint, Chinese7937ItemCatalogRow> mainById;
    private readonly FrozenDictionary<uint, Chinese7937ItemCatalogRow> subById;

    private Chinese7937EchoRuntimeCatalog(ChineseFullClientCatalogSnapshot captured,
        Chinese7937EchoRequirements requirements, Chinese7937ItemCatalogDocument main,
        Chinese7937ItemCatalogDocument sub)
    {
        Captured = captured;
        Requirements = requirements;
        mainById = main.Rows.ToFrozenDictionary(row => row.ItemId);
        subById = sub.Rows.ToFrozenDictionary(row => row.ItemId);
    }

    public ChineseFullClientCatalogSnapshot Captured { get; }
    public Chinese7937EchoRequirements Requirements { get; }
    public int MainItemCount => mainById.Count;
    public int SubItemCount => subById.Count;

    /// <summary>Preserves both catalog locations without guessing precedence or non-ID column semantics.</summary>
    public bool TryFindItem(uint itemType, out Chinese7937ItemCatalogReference reference)
    {
        mainById.TryGetValue(itemType, out var main);
        subById.TryGetValue(itemType, out var sub);
        reference = new(itemType, main, sub);
        return reference.IsPresent;
    }

    private bool AllItemReferencesResolve()
    {
        foreach (var row in Requirements.CostRows)
            if (!TryFindItem(row.ItemTypeField, out _)) return false;
        foreach (var row in Requirements.OpeningRows)
            if (!TryFindItem(row.ItemType1Field, out _) || !TryFindItem(row.ItemType2Field, out _)) return false;
        return true;
    }
}

public readonly record struct Chinese7937ItemCatalogReference(uint ItemType,
    Chinese7937ItemCatalogRow? Main, Chinese7937ItemCatalogRow? Sub)
{
    public bool IsPresent => Main is not null || Sub is not null;
}

public enum Chinese7937EchoRuntimeCatalogFailure
{
    None, SourceRequired, UnsupportedVersion, ItemEnvelopeRequired, InfoFindingRequired,
    PinRequired, KeyRequired, InputBounds, PinMismatch, InvalidInfo, UnsupportedRequirements,
    MissingItemReference,
}
