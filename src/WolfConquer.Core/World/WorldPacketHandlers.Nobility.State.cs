using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ulong LegacyNobilityKnightDonation = 30_000_000;

    private const ulong LegacyNobilityBaronDonation = 100_000_000;

    private const ulong LegacyNobilityEarlDonation = 200_000_000;

    private const byte LegacyNobilityRankSerf = 0;

    private const byte LegacyNobilityRankKnight = 1;

    private const byte LegacyNobilityRankBaron = 3;

    private const byte LegacyNobilityRankEarl = 5;

    private const byte LegacyNobilityRankKing = 12;

    private const int LegacyAccountManagerPaidNobilityDays = 30;


    private static ulong ReadLegacyNobilityRank(CharacterSnapshot character)
    {
        var numeric = ReadAttributeUInt64(character.Attributes, "NobilityRank", "Nobility");
        if (numeric != 0)
        {
            return numeric;
        }

        if (!character.PaidNobility.TryGetValue("Rank", out var rank) ||
            string.IsNullOrWhiteSpace(rank))
        {
            return 0;
        }

        return rank.Trim() switch
        {
            "Knight" => 1,
            "Baron" => 3,
            "Earl" => 5,
            "Duke" => 7,
            "Prince" => 9,
            "King" => 12,
            _ => 0,
        };
    }


    private static ulong ReadLegacyNobilityDonation(CharacterSnapshot character)
    {
        return LegacyNobilityRanking.ReadRegularDonation(character);
    }


    private static CharacterSnapshot WithLegacyNobilityDonation(CharacterSnapshot character, ulong totalDonation)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = totalDonation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = totalDonation.ToString(CultureInfo.InvariantCulture),
        };

        return character with { Attributes = attributes };
    }


    private static CharacterSnapshot WithLegacyNobilityState(
        CharacterSnapshot character,
        LegacyNobilityState state)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = state.Donation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = state.Donation.ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = state.Rank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = state.Position.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobility = new Dictionary<string, string>(
            character.PaidNobility,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = character.Uid.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = state.Donation.ToString(CultureInfo.InvariantCulture),
            ["PaidRank"] = state.Rank.ToString(CultureInfo.InvariantCulture),
            ["Rank"] = LegacyNobilityRanking.RankName(state.Rank),
            ["Position"] = state.Position.ToString(CultureInfo.InvariantCulture),
        };

        return character with
        {
            Attributes = attributes,
            PaidNobility = paidNobility,
        };
    }


    private static CharacterSnapshot WithLegacyPaidNobilityPurchase(
        CharacterSnapshot character,
        ulong donatePoints,
        ulong previousDonation,
        byte rank,
        int position,
        long periodTime)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = donatePoints.ToString(CultureInfo.InvariantCulture),
            ["DonationNobility"] = "0",
            ["LastNobilityDonation"] = "0",
            ["NobilityRank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = position.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobility = new Dictionary<string, string>(
            character.PaidNobility,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = character.Uid.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = previousDonation.ToString(CultureInfo.InvariantCulture),
            ["PaidRank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["PeriodTime"] = periodTime.ToString(CultureInfo.InvariantCulture),
            ["IsActive"] = bool.TrueString,
            ["Rank"] = LegacyNobilityRanking.RankName(rank),
            ["Position"] = position.ToString(CultureInfo.InvariantCulture),
        };

        return character with
        {
            Attributes = attributes,
            PaidNobility = paidNobility,
        };
    }


    private static int LegacyPaidNobilityPosition(byte rank)
    {
        return rank switch
        {
            LegacyNobilityRanking.RankKing => 0,
            LegacyNobilityRanking.RankPrince => 4,
            LegacyNobilityRanking.RankDuke => 15,
            _ => LegacyNobilityRanking.DefaultPosition,
        };
    }


    private static byte ComputeLegacyNobilityRank(ulong donation)
    {
        return LegacyNobilityRanking.ComputeRank(donation, LegacyNobilityRanking.DefaultPosition);
    }


    private static string LegacyNobilityRankName(byte rank)
    {
        return LegacyNobilityRanking.RankName(rank);
    }
}
