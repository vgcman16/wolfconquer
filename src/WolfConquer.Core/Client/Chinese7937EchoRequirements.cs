using System.Collections.ObjectModel;
using System.Globalization;

namespace WolfConquer.Core.Client;

public readonly record struct Chinese7937EchoCostRow(
    uint HoleNumber, uint MoneyField, uint ItemTypeField, uint ItemNumberField);

public readonly record struct Chinese7937EchoOpeningRow(
    uint HoleNumber, uint CostNumberField, uint ItemType1Field, uint Experience1Field,
    uint ItemType2Field, uint Experience2Field, uint MaximumExperienceField);

/// <summary>Complete typed projection of the observed7937 selected Info literals; values grant no gameplay authority.</summary>
public sealed class Chinese7937EchoRequirements
{
    public const int KnownFieldCount = 59;
    public const uint MaximumKnownHoleNumber = 7;
    private readonly ReadOnlyCollection<Chinese7937EchoCostRow> costRows;
    private readonly ReadOnlyCollection<Chinese7937EchoOpeningRow> openingRows;

    private Chinese7937EchoRequirements(Chinese7937XuanbaoInfoSnapshot source, uint maximum, uint costNumber,
        Chinese7937EchoCostRow[] costs, Chinese7937EchoOpeningRow[] openings)
    {
        Source = source;
        CatalogMaximumHoleNumber = maximum;
        CostNumberField = costNumber;
        costRows = Array.AsReadOnly(costs);
        openingRows = Array.AsReadOnly(openings);
    }

    public Chinese7937XuanbaoInfoSnapshot Source { get; }
    public uint CatalogMaximumHoleNumber { get; }
    public uint CostNumberField { get; }
    public IReadOnlyList<Chinese7937EchoCostRow> CostRows => costRows;
    public IReadOnlyList<Chinese7937EchoOpeningRow> OpeningRows => openingRows;

    public static bool TryCreate(Chinese7937XuanbaoInfoSnapshot info,
        out Chinese7937EchoRequirements? requirements, out Chinese7937EchoRequirementsFailure failure)
    {
        requirements = null;
        if (info is null) { failure = Chinese7937EchoRequirementsFailure.SourceRequired; return false; }
        if (info.DeclaredVersion != Chinese7937XuanbaoInfoVersion.Catalog7937)
        { failure = Chinese7937EchoRequirementsFailure.UnsupportedVersion; return false; }
        if (info.Count != KnownFieldCount || !info.TryGetValue("max_hole_num", out var maximum) ||
            !info.TryGetValue("cost_num", out var costNumber) ||
            !TryReadCosts(info, out var costs) || !TryReadOpenings(info, out var openings))
        { failure = Chinese7937EchoRequirementsFailure.UnsupportedShape; return false; }
        if (maximum is < 1 or > MaximumKnownHoleNumber)
        { failure = Chinese7937EchoRequirementsFailure.MaximumHoleBounds; return false; }
        requirements = new(info, maximum, costNumber, costs, openings);
        failure = Chinese7937EchoRequirementsFailure.None;
        return true;
    }

    /// <summary>Returns retained catalog data, including rows beyond the named UI maximum; grants no activation permission.</summary>
    public bool TryGetCost(uint holeNumber, out Chinese7937EchoCostRow row)
    {
        row = default;
        if (holeNumber is < 1 or > MaximumKnownHoleNumber) return false;
        row = costRows[(int)holeNumber - 1];
        return true;
    }

    public bool TryGetOpening(uint holeNumber, out Chinese7937EchoOpeningRow row)
    {
        row = default;
        if (holeNumber is < 2 or > MaximumKnownHoleNumber) return false;
        row = openingRows[(int)holeNumber - 2];
        return true;
    }

    private static bool TryReadCosts(Chinese7937XuanbaoInfoSnapshot info, out Chinese7937EchoCostRow[] rows)
    {
        rows = new Chinese7937EchoCostRow[MaximumKnownHoleNumber];
        for (uint hole = 1; hole <= MaximumKnownHoleNumber; hole++)
        {
            var suffix = hole.ToString(CultureInfo.InvariantCulture);
            if (!info.TryGetValue("cost_money_" + suffix, out var money) ||
                !info.TryGetValue("cost_itemtype_" + suffix, out var itemType) ||
                !info.TryGetValue("cost_itemnum_" + suffix, out var itemNumber)) return false;
            rows[(int)hole - 1] = new(hole, money, itemType, itemNumber);
        }
        return true;
    }

    private static bool TryReadOpenings(Chinese7937XuanbaoInfoSnapshot info, out Chinese7937EchoOpeningRow[] rows)
    {
        rows = new Chinese7937EchoOpeningRow[MaximumKnownHoleNumber - 1];
        for (uint hole = 2; hole <= MaximumKnownHoleNumber; hole++)
        {
            var prefix = "open_hole" + hole.ToString(CultureInfo.InvariantCulture) + "_";
            if (!info.TryGetValue(prefix + "cost_num", out var costNumber) ||
                !info.TryGetValue(prefix + "itemtype_1", out var itemType1) ||
                !info.TryGetValue(prefix + "exp_1", out var experience1) ||
                !info.TryGetValue(prefix + "itemtype_2", out var itemType2) ||
                !info.TryGetValue(prefix + "exp_2", out var experience2) ||
                !info.TryGetValue(prefix + "maxexp", out var maximumExperience)) return false;
            rows[(int)hole - 2] = new(hole, costNumber, itemType1, experience1,
                itemType2, experience2, maximumExperience);
        }
        return true;
    }
}

public enum Chinese7937EchoRequirementsFailure
{
    None, SourceRequired, UnsupportedVersion, UnsupportedShape, MaximumHoleBounds,
}
