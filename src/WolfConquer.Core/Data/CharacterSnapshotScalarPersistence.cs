using System.Globalization;

namespace WolfConquer.Core.Data;

public static class CharacterSnapshotScalarPersistence
{
    private const string LegacyMainFlagStorageKey = "ClaimStateGift";
    private const string LegacyPhoenixChiStorageKey = "Pheonix";
    private const string RuntimeMainFlagAttribute = "MainFlag";
    private const string RuntimePhoenixChiAttribute = "Phoenix";

    private static readonly string[] PersistedAttributeKeys =
    [
        RuntimeMainFlagAttribute,
        "CanChangeWindWalkerFree",
        "ClassExperience",
        "WHMoney",
        "VotePoints",
        "PinCodeAnima",
        "Dragon",
        "Tiger",
        "Turtle",
        "ChiPoints",
        "DragonTime",
        "PhoenixTime",
        "TigerTime",
        "TurtleTime",
        "RetreatedDragon",
        "RetreatedPhoenix",
        "RetreatedTiger",
        "RetreatedTurtle",
        "SkyTowerCompletedChallengeId",
        "SkyTowerAvailableAttempts",
    ];

    public static bool HasPersistedScalarChanges(CharacterSnapshot previous, CharacterSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        return previous.Body != current.Body ||
            previous.Face != current.Face ||
            previous.Hair != current.Hair ||
            previous.Class != current.Class ||
            previous.FirstClass != current.FirstClass ||
            previous.SecondClass != current.SecondClass ||
            previous.Reborn != current.Reborn ||
            previous.Level != current.Level ||
            previous.Experience != current.Experience ||
            previous.MapId != current.MapId ||
            previous.X != current.X ||
            previous.Y != current.Y ||
            previous.PreviousMapId != current.PreviousMapId ||
            previous.PreviousMapX != current.PreviousMapX ||
            previous.PreviousMapY != current.PreviousMapY ||
            previous.Strength != current.Strength ||
            previous.Agility != current.Agility ||
            previous.Vitality != current.Vitality ||
            previous.Spirit != current.Spirit ||
            previous.AttributePoints != current.AttributePoints ||
            previous.MinHitPoints != current.MinHitPoints ||
            previous.MinMana != current.MinMana ||
            previous.MaxMana != current.MaxMana ||
            previous.Money != current.Money ||
            previous.ConquerPoints != current.ConquerPoints ||
            previous.BoundConquerPoints != current.BoundConquerPoints ||
            previous.BankConquerPoints != current.BankConquerPoints ||
            previous.PkPoints != current.PkPoints ||
            previous.Online != current.Online ||
            previous.ExpBallUsed != current.ExpBallUsed ||
            HasPersistedAttributeChanges(previous, current);
    }

    public static bool TryPersist(
        string? usersRoot,
        CharacterSnapshot snapshot,
        out string path,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        path = string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        if (CharacterSnapshotEpicTrojanSchema.HasMalformedStoredComposite(snapshot.Attributes))
        {
            reason = "epicTrojanMalformed";
            return false;
        }

        path = Path.Combine(usersRoot, snapshot.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(path))
        {
            reason = "snapshotMissing";
            return false;
        }

        return CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
            path,
            snapshot.Uid,
            BuildPersistedValues(snapshot),
            detailedIoFailures: true,
            out reason);
    }

    internal static IReadOnlyDictionary<string, string> BuildPersistedValues(CharacterSnapshot snapshot)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UID"] = Format(snapshot.Uid),
            ["Body"] = Format(snapshot.Body),
            ["Face"] = Format(snapshot.Face),
            ["Class"] = Format(snapshot.Class),
            ["FirstClass"] = Format(snapshot.FirstClass),
            ["SecoundeClass"] = Format(snapshot.SecondClass),
            ["Reborn"] = Format(snapshot.Reborn),
            ["Level"] = Format(snapshot.Level),
            ["Haire"] = Format(snapshot.Hair),
            ["Experience"] = Format(snapshot.Experience),
            ["Map"] = Format(snapshot.MapId),
            ["X"] = Format(snapshot.X),
            ["Y"] = Format(snapshot.Y),
            ["PMap"] = Format(snapshot.PreviousMapId),
            ["PMapX"] = Format(snapshot.PreviousMapX),
            ["PMapY"] = Format(snapshot.PreviousMapY),
            ["Agility"] = Format(snapshot.Agility),
            ["Strength"] = Format(snapshot.Strength),
            ["Vitaliti"] = Format(snapshot.Vitality),
            ["Spirit"] = Format(snapshot.Spirit),
            ["Atributes"] = Format(snapshot.AttributePoints),
            ["MinHitPoints"] = Format(snapshot.MinHitPoints),
            ["MinMana"] = Format(snapshot.MinMana),
            ["MaxMana"] = Format(snapshot.MaxMana),
            ["ConquerPoints"] = Format(snapshot.ConquerPoints),
            ["BankCps"] = Format(snapshot.BankConquerPoints),
            ["BoundConquerPoints"] = Format(snapshot.BoundConquerPoints),
            ["Money"] = Format(snapshot.Money),
            ["PkPoints"] = Format(snapshot.PkPoints),
            ["Online"] = Format(snapshot.Online),
            ["ExpBallUsed"] = Format(snapshot.ExpBallUsed),
        };

        foreach (var key in PersistedAttributeKeys)
        {
            if (snapshot.Attributes.TryGetValue(key, out var value))
            {
                var storageKey = key == RuntimeMainFlagAttribute
                    ? LegacyMainFlagStorageKey
                    : key;
                values[storageKey] = value;
            }
        }

        if (TryGetPhoenixChiValue(snapshot.Attributes, out var phoenix))
        {
            values[LegacyPhoenixChiStorageKey] = phoenix;
        }

        if (CharacterSnapshotEpicTrojanSchema.TryBuildPersistedComposite(
                snapshot.Attributes,
                out var epicTrojan))
        {
            values[CharacterSnapshotEpicTrojanSchema.StorageAttributeKey] = epicTrojan;
        }

        return values;
    }

    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool HasPersistedAttributeChanges(CharacterSnapshot previous, CharacterSnapshot current)
    {
        foreach (var key in PersistedAttributeKeys)
        {
            var previousValue = previous.Attributes.TryGetValue(key, out var previousRaw) ? previousRaw : null;
            var currentValue = current.Attributes.TryGetValue(key, out var currentRaw) ? currentRaw : null;
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var previousHasPhoenix = TryGetPhoenixChiValue(previous.Attributes, out var previousPhoenix);
        var currentHasPhoenix = TryGetPhoenixChiValue(current.Attributes, out var currentPhoenix);
        if (previousHasPhoenix != currentHasPhoenix ||
            !string.Equals(previousPhoenix, currentPhoenix, StringComparison.Ordinal))
        {
            return true;
        }

        return CharacterSnapshotEpicTrojanSchema.HasModeledRuntimeChanges(
            previous.Attributes,
            current.Attributes);
    }

    private static bool TryGetPhoenixChiValue(
        IReadOnlyDictionary<string, string> attributes,
        out string value)
    {
        return attributes.TryGetValue(LegacyPhoenixChiStorageKey, out value!) ||
            attributes.TryGetValue(RuntimePhoenixChiAttribute, out value!);
    }
}
