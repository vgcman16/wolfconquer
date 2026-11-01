using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only named event fields are edited. Unrelated case-distinct attributes
    // retain their original comparer, keys, values and insertion order.
    private static Dictionary<string, string> CopyOwnedSobEventAttributes(CharacterSnapshot character, string[] names)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var selected = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(proof.CapturedComparer);
        foreach (var row in proof.Fields)
        {
            if (selected.Contains(row.Key))
            {
                if (!seen.Add(row.Key)) throw new InvalidDataException("ownedDragonhowlEventAttributeAmbiguous");
            }
            else result.Add(row.Key, row.Value);
        }
        return result;
    }

    private static string ReadOwnedSobEventAttribute(CharacterSnapshot character, params string[] names)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var selected = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? value = null;
        foreach (var row in proof.Fields)
        {
            if (!selected.Contains(row.Key)) continue;
            if (!seen.Add(row.Key) || value is not null && !StringComparer.Ordinal.Equals(value, row.Value))
                throw new InvalidDataException("ownedDragonhowlEventAttributeAmbiguous");
            value = row.Value;
        }
        return value ?? string.Empty;
    }

    private static bool TryPrepareDragonhowlCaptureTheFlag(WorldSession actor, SobNpc original,
        SobNpc after, SobNpcDamageResult result, out WorldSession projected, out SobNpc? reset,
        out string? resetName, out uint? resetGuild)
    {
        projected = actor; reset = null; resetName = null; resetGuild = null;
        if (actor.Character.MapId != LegacyCaptureTheFlagMapId || original.MapId != LegacyCaptureTheFlagMapId ||
            original.Id < 0 || result.DamageApplied == 0) return false;
        var character = actor.Character;
        var dynamicText = ReadOwnedSobEventAttribute(character, "DynamicID", "DynamicId");
        if (dynamicText.Length != 0 && (!ulong.TryParse(dynamicText, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out var dynamicId) || dynamicId != 0)) return false;
        if (!ulong.TryParse(ReadOwnedSobEventAttribute(character, "GuildID", "GuildId"), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var guildId) || guildId == 0) return false;
        var guildName = ReadOwnedSobEventAttribute(character, "GuildName").Trim();
        if (guildName.Length == 0) return false;
        var selected = ValidateDragonhowlCtfInputs(character);
        var bases = ReadCaptureTheFlagBases(selected);
        var baseUid = checked((uint)original.Id);
        var baseIndex = bases.FindIndex(row => row.Uid == baseUid && row.Viable);
        if (baseIndex < 0) return false;
        var scores = ReadCaptureTheFlagBaseScores(selected);
        var scoreIndex = scores.FindIndex(row => row.BaseUid == baseUid && row.GuildId == guildId);
        if (scoreIndex >= 0)
            scores[scoreIndex] = scores[scoreIndex] with { GuildName = guildName,
                Score = SaturatingAddUInt32(scores[scoreIndex].Score, result.DamageApplied) };
        else scores.Add(new(baseUid, guildId, guildName, result.DamageApplied));
        projected = actor with { Character = WithCaptureTheFlagBaseScoresAttribute(character, scores) };
        if (!result.Killed) return true;
        if (bases[baseIndex].CapturerGuildId == guildId) return false;
        var winner = scores.Where(row => row.BaseUid == baseUid)
            .OrderByDescending(row => row.Score).ThenBy(row => row.GuildId).First();
        bases[baseIndex] = bases[baseIndex] with { CapturerGuildId = winner.GuildId };
        projected = projected with { Character = WithCaptureTheFlagBaseScoresAttribute(
            WithCaptureTheFlagBasesAttribute(projected.Character, bases), scores.Where(row => row.BaseUid != baseUid).ToArray()) };
        resetName = winner.GuildName;
        resetGuild = winner.GuildId <= uint.MaxValue ? (uint)winner.GuildId : null;
        reset = after with { Life = after.MaxLife, Name = winner.GuildName };
        return true;
    }

    private static CharacterSnapshot ValidateDragonhowlCtfInputs(CharacterSnapshot character)
    {
        var bases = ReadOwnedSobEventAttribute(character, "CaptureTheFlagBases", "CtfBases", "CTFBases");
        var scores = ReadOwnedSobEventAttribute(character, "CaptureTheFlagBaseScores", "CtfBaseScores", "CTFBaseScores");
        // Canonical selected-field view for the existing pure row parsers only.
        // It is never published and cannot invoke a Guilds-file fallback.
        var selected = character with { Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["CaptureTheFlagBases"] = bases, ["CaptureTheFlagBaseScores"] = scores } };
        var rows = bases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = ReadCaptureTheFlagBases(selected);
        if (rows.Length != parsed.Count || parsed.Select(row => row.Uid).Distinct().Count() != parsed.Count)
            throw new InvalidDataException("ownedDragonhowlCtfBasesInvalid");
        foreach (var row in rows)
        {
            var fields = row.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length is not (5 or 6) || fields.Length == 6 && !TryParseLegacyBool(fields[5], out _))
                throw new InvalidDataException("ownedDragonhowlCtfBasesInvalid");
        }
        var scoreRows = scores.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsedScores = ReadCaptureTheFlagBaseScores(selected);
        if (scoreRows.Length != parsedScores.Count ||
            parsedScores.Select(row => (row.BaseUid, row.GuildId)).Distinct().Count() != parsedScores.Count)
            throw new InvalidDataException("ownedDragonhowlCtfScoresInvalid");
        return selected;
    }
}
