using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static bool TryReadExpiredLegacyPaidNobility(
        CharacterSnapshot character,
        DateTimeOffset now,
        out ulong restoredDonation)
    {
        restoredDonation = 0;
        if (!TryIsActiveLegacyPaidNobility(character.PaidNobility, out var active) ||
            !active ||
            !TryReadInt64(character.PaidNobility, "PeriodTime", out var periodTimeBinary))
        {
            return false;
        }

        DateTime periodTime;
        try
        {
            periodTime = DateTime.FromBinary(periodTimeBinary);
        }
        catch (ArgumentException)
        {
            periodTime = DateTime.MinValue;
        }

        if (now.LocalDateTime < periodTime)
        {
            return false;
        }

        TryReadUInt64(character.PaidNobility, "LastNobilityDonation", out restoredDonation);
        return true;
    }

    private static bool TryIsActiveLegacyPaidNobility(
        IReadOnlyDictionary<string, string> paidNobility,
        out bool active)
    {
        active = false;
        if (!paidNobility.TryGetValue("IsActive", out var raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out active))
        {
            return true;
        }

        if (byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            active = numeric != 0;
            return true;
        }

        return false;
    }

    private static CharacterSnapshot WithExpiredLegacyPaidNobilityState(
        CharacterSnapshot character,
        ulong restoredDonation,
        byte rank,
        int position)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = restoredDonation.ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = position.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobility = new Dictionary<string, string>(
            character.PaidNobility,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = "0",
            ["LastNobilityDonation"] = "0",
            ["PaidRank"] = "0",
            ["PeriodTime"] = "0",
            ["IsActive"] = bool.FalseString,
            ["Rank"] = "Serf",
            ["Position"] = LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
        };

        return character with
        {
            Attributes = attributes,
            PaidNobility = paidNobility,
        };
    }

    private static CharacterSnapshot WithLegacyNobilityStateAttributes(
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

        return character with { Attributes = attributes };
    }

    private async ValueTask BroadcastLegacyPaidNobilityPromotionAsync(
        LegacyNobilityState donor,
        CancellationToken cancellationToken)
    {
        var frame = LegacyMsgMessagePacket
            .SystemRedCenter($"{donor.Name} has been promoted to {LegacyNobilityRanking.RankName(donor.Rank)}.")
            .ToFrameBytes();
        foreach (var active in _sessions.Snapshot())
        {
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryPersistExpiredLegacyPaidNobility(
        string? usersRoot,
        long accountId,
        CharacterSnapshot character,
        out string path,
        out string reason)
    {
        path = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        path = Path.Combine(usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(path))
        {
            reason = "snapshotMissing";
            return false;
        }

        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonationNobility"] = LegacyNobilityRanking.ReadRegularDonation(character).ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = LegacyNobilityRanking.ReadRegularDonation(character).ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = ReadSnapshotUInt64(character.Attributes, "NobilityRank").ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = character.Attributes.TryGetValue("NobilityPosition", out var nobilityPosition)
                ? nobilityPosition
                : LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = "0",
            ["LastNobilityDonation"] = "0",
            ["PaidRank"] = "0",
            ["PeriodTime"] = "0",
            ["IsActive"] = bool.FalseString,
            ["Rank"] = "Serf",
            ["Position"] = LegacyNobilityRanking.DefaultPosition.ToString(CultureInfo.InvariantCulture),
        };

        return CharacterSnapshotFileCoordinator.TryUpdateSections(
            path,
            character.Uid,
            [
                new CharacterSnapshotSectionUpdate(
                    "Character",
                    characterValues,
                    CreateIfMissing: false,
                    MissingReason: "characterSectionMissing"),
                new CharacterSnapshotSectionUpdate(
                    "PaidNobility",
                    paidNobilityValues,
                    CreateIfMissing: true,
                    MissingReason: "paidNobilitySectionMissing"),
            ],
            detailedIoFailures: false,
            out reason);
    }

    private static bool TryReadUInt64(
        IReadOnlyDictionary<string, string> values,
        string key,
        out ulong parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static bool TryReadInt64(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long parsed)
    {
        parsed = 0;
        return values.TryGetValue(key, out var value) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static ulong ReadSnapshotUInt64(IReadOnlyDictionary<string, string> values, string key)
    {
        return TryReadUInt64(values, key, out var parsed) ? parsed : 0;
    }
}
