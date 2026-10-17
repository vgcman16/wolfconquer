using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyChiBuyPointsMutation(
    CharacterSnapshot Character,
    int PointsPurchased,
    uint ConquerPointsSpent,
    uint MaximumChiPoints);

public static class LegacyChiBuyPointsState
{
    private const uint BaseMaximumChiPoints = 4_000;
    private const int MaximumChiPointsValueIndex = 9;

    public static bool TryBuyToMaximum(
        CharacterSnapshot character,
        IReadOnlyDictionary<int, IReadOnlyList<FateExperience>>? fateExperienceByType,
        bool legacyTradeActive,
        out LegacyChiBuyPointsMutation mutation,
        out uint requiredConquerPoints,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        mutation = new LegacyChiBuyPointsMutation(
            character,
            PointsPurchased: 0,
            ConquerPointsSpent: 0,
            MaximumChiPoints: 0);
        requiredConquerPoints = 0;

        if (fateExperienceByType is null)
        {
            reason = "fateExperienceMissing";
            return false;
        }

        var state = LegacyChiState.FromSnapshot(character);
        var maximumChiPoints = BaseMaximumChiPoints;
        foreach (var power in state.Powers)
        {
            if (!fateExperienceByType.TryGetValue((int)power.Type, out var fateRows))
            {
                reason = "fateExperienceMissing";
                return false;
            }

            var currentFate = fateRows
                .Where(row => row.Experience >= 0 && (ulong)row.Experience <= power.Experience)
                .OrderBy(row => row.Experience)
                .LastOrDefault();
            if (currentFate is null)
            {
                continue;
            }

            if (currentFate.Values.Count <= MaximumChiPointsValueIndex ||
                currentFate.Values[MaximumChiPointsValueIndex] is < 0 or > uint.MaxValue)
            {
                reason = "maximumChiPointsInvalid";
                return false;
            }

            var bonus = (uint)currentFate.Values[MaximumChiPointsValueIndex];
            if (bonus > uint.MaxValue - maximumChiPoints)
            {
                reason = "maximumChiPointsInvalid";
                return false;
            }

            maximumChiPoints += bonus;
        }

        mutation = mutation with { MaximumChiPoints = maximumChiPoints };
        if (state.ChiPoints < 0)
        {
            reason = "chiPointsInvalid";
            return false;
        }

        if ((ulong)state.ChiPoints >= maximumChiPoints)
        {
            reason = "chiPointsFull";
            return false;
        }

        var pointsToBuy = maximumChiPoints - (uint)state.ChiPoints;
        if (pointsToBuy > int.MaxValue)
        {
            reason = "chiPointsPurchaseInvalid";
            return false;
        }

        var cost = Math.Ceiling((pointsToBuy / 50u) * 12.5d);
        if (cost is < 0 or > uint.MaxValue)
        {
            reason = "conquerPointsCostInvalid";
            return false;
        }

        requiredConquerPoints = (uint)cost;
        if (character.ConquerPoints < requiredConquerPoints)
        {
            reason = "conquerPointsInsufficient";
            return false;
        }

        if (legacyTradeActive)
        {
            reason = "tradeActive";
            return false;
        }

        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ChiPoints"] = maximumChiPoints.ToString(CultureInfo.InvariantCulture),
        };
        mutation = new LegacyChiBuyPointsMutation(
            character with
            {
                ConquerPoints = character.ConquerPoints - requiredConquerPoints,
                Attributes = attributes,
            },
            checked((int)pointsToBuy),
            requiredConquerPoints,
            maximumChiPoints);
        reason = "boughtPoints";
        return true;
    }
}
