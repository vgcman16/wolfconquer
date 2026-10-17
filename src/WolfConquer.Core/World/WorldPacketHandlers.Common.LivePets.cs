using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static IReadOnlyList<LivePetProjection> ReadLivePets(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "LivePets", "ListPets", "OwnerPets");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0)
        {
            return [];
        }

        var pets = new List<LivePetProjection>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 7 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
                !uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mesh) ||
                !uint.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var petFlag) ||
                !uint.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId) ||
                !byte.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ||
                !uint.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hitpoints) ||
                !uint.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxLife))
            {
                continue;
            }

            var name = fields.Length > 7 ? fields[7] : string.Empty;
            ushort? x = null;
            ushort? y = null;
            if (fields.Length > 9 &&
                ushort.TryParse(fields[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedX) &&
                ushort.TryParse(fields[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedY))
            {
                x = parsedX;
                y = parsedY;
            }

            var nextAttackAtUtc = fields.Length > 10
                ? ParseLegacyLivePetTimestamp(fields[10])
                : null;
            var thunderboltExpiresAtUtc = fields.Length > 11
                ? ParseLegacyLivePetTimestamp(fields[11])
                : null;
            pets.Add(new LivePetProjection(
                uid,
                mesh,
                petFlag,
                monsterId,
                level,
                hitpoints,
                maxLife,
                name,
                x,
                y,
                nextAttackAtUtc,
                thunderboltExpiresAtUtc));
        }

        return pets;
    }

    private static CharacterSnapshot WithLegacyLivePetProjections(
        CharacterSnapshot character,
        IReadOnlyList<LivePetProjection> pets)
    {
        if (pets.Count == 0)
        {
            return WithoutLegacyLivePetProjections(character);
        }

        var entries = pets.Select(pet =>
        {
            var fields = new List<string>
            {
                pet.Uid.ToString(CultureInfo.InvariantCulture),
                pet.Mesh.ToString(CultureInfo.InvariantCulture),
                pet.PetFlag.ToString(CultureInfo.InvariantCulture),
                pet.MonsterId.ToString(CultureInfo.InvariantCulture),
                pet.Level.ToString(CultureInfo.InvariantCulture),
                pet.Hitpoints.ToString(CultureInfo.InvariantCulture),
                pet.MaxLife.ToString(CultureInfo.InvariantCulture),
                pet.Name,
            };

            var hasCoordinates = pet.X.HasValue && pet.Y.HasValue;
            if (hasCoordinates)
            {
                fields.Add(pet.X.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
                fields.Add(pet.Y.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
            }

            if (pet.NextAttackAtUtc.HasValue || pet.ThunderboltExpiresAtUtc.HasValue)
            {
                if (!hasCoordinates)
                {
                    fields.Add("-");
                    fields.Add("-");
                }

                fields.Add(FormatLegacyLivePetTimestamp(pet.NextAttackAtUtc));
                fields.Add(FormatLegacyLivePetTimestamp(pet.ThunderboltExpiresAtUtc));
            }

            return string.Join(',', fields);
        });

        return WithCharacterAttribute(character, "LivePets", string.Join(';', entries));
    }

    private static CharacterSnapshot WithoutLegacyLivePetProjections(CharacterSnapshot character)
    {
        return WithoutCharacterAttribute(
            WithoutCharacterAttribute(
                WithoutCharacterAttribute(character, "LivePets"),
                "ListPets"),
            "OwnerPets");
    }

    private static DateTimeOffset? ParseLegacyLivePetTimestamp(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMilliseconds) ||
            unixMilliseconds == 0)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static string FormatLegacyLivePetTimestamp(DateTimeOffset? value)
    {
        return value?.ToUniversalTime().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "0";
    }
}
