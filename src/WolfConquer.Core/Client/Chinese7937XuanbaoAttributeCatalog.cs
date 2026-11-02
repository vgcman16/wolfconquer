using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace WolfConquer.Core.Client;

public readonly record struct Chinese7937XuanbaoScoreLimit(uint ItemType, uint AttributeType, uint Index, uint Value);
public readonly record struct Chinese7937XuanbaoGoldLimitFlag(uint ItemType, uint AttributeType, bool Value);

/// <summary>Owned selected7937 literal data; sparse presence is independent from zero/false values.</summary>
public sealed class Chinese7937XuanbaoAttributeCatalog
{
    private readonly ReadOnlyCollection<Chinese7937XuanbaoScoreLimit> scoreLimits;
    private readonly ReadOnlyCollection<Chinese7937XuanbaoGoldLimitFlag> goldLimitFlags;
    private readonly ReadOnlyCollection<uint> goldLimitItemTypes;
    private readonly FrozenDictionary<(uint Item, uint Attribute, uint Index), uint> scoreByKey;
    private readonly FrozenDictionary<(uint Item, uint Attribute), bool> goldByKey;
    private readonly FrozenSet<uint> goldItems;

    internal Chinese7937XuanbaoAttributeCatalog(string contentSha256,
        Chinese7937XuanbaoScoreLimit[] scores, Chinese7937XuanbaoGoldLimitFlag[] flags)
    {
        ContentSha256 = contentSha256;
        var ownedScores = scores.ToArray();
        var ownedFlags = flags.ToArray();
        scoreLimits = Array.AsReadOnly(ownedScores);
        goldLimitFlags = Array.AsReadOnly(ownedFlags);
        goldLimitItemTypes = Array.AsReadOnly(ownedFlags.Select(row => row.ItemType).Distinct().ToArray());
        scoreByKey = ownedScores.ToFrozenDictionary(row => (row.ItemType, row.AttributeType, row.Index), row => row.Value);
        goldByKey = ownedFlags.ToFrozenDictionary(row => (row.ItemType, row.AttributeType), row => row.Value);
        goldItems = goldLimitItemTypes.ToFrozenSet();
    }

    public int ClientVersion => 7937;
    public string ContentSha256 { get; }
    public IReadOnlyList<Chinese7937XuanbaoScoreLimit> ScoreLimits => scoreLimits;
    public IReadOnlyList<Chinese7937XuanbaoGoldLimitFlag> GoldLimitFlags => goldLimitFlags;
    public IReadOnlyList<uint> GoldLimitItemTypes => goldLimitItemTypes;
    public bool HasGoldLimitItem(uint itemType) => goldItems.Contains(itemType);
    public bool TryGetGoldLimitFlag(uint itemType, uint attributeType, out bool value) =>
        goldByKey.TryGetValue((itemType, attributeType), out value);
    public bool TryGetScoreLimit(uint itemType, uint attributeType, uint index, out uint value) =>
        scoreByKey.TryGetValue((itemType, attributeType, index), out value);
}
