using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyCapturedCharacterPrefix
{
    private static bool TryParseCaptured(SqlWorldStateImage image, CharacterSnapshotFileBaseline.Snapshot baseline,
        out CharacterSnapshot? snapshot, out LegacyNativePrefixValues? values, out bool beastActivated,
        out uint beastTotalPoints, out uint beastFlag, out string? hairfaceLine, out string reason)
    {
        snapshot = null; values = null; beastActivated = false; beastTotalPoints = 0; beastFlag = 0; hairfaceLine = null;
        reason = "capturedPrefixInvalidData";
        var fields = baseline.Fields;
        string[] recognized = ["UID", "Class", "Level", "Online", "Name", "Spouse", "Body", "Face",
            "RelicSpirit", "SpawinEffect", "SpawinEffectSkills", "DeityLandLuckyPoints", "HeroPoints",
            "ClassExperience", "SavePromote", "SkinUnlock", "DoomosaurSkin", "PrayingElfSkin",
            "PolarDragonSkin", "ChargeChi", "ChargeGuildSkill", "TailedBeastActivated", "TailedBeastTotalPoints",
            "TailedBeastFlag", "HairfaceStorage"];
        foreach (string key in recognized)
        {
            if (!fields.TryGetValue(key, out var raw)) continue;
            var trimmed = raw.Trim();
            bool quoted = trimmed.Length >= 2 && ((trimmed[0] == '"' && trimmed[^1] == '"') ||
                (trimmed[0] == '\'' && trimmed[^1] == '\''));
            if (quoted || trimmed.Any(char.IsControl))
            { reason = "capturedPrefixQuotedOrControlValue"; return false; }
        }
        string name = fields.TryGetValue("Name", out var savedName) ? savedName.Trim() : string.Empty;
        string spouse = fields.TryGetValue("Spouse", out var savedSpouse) ? savedSpouse.Trim() : "None";
        if (name.Length > 254 || spouse.Length > 254)
        { reason = "capturedPrefixNameOrSpouseTooLong"; return false; }
        string hairfaceRaw = fields.TryGetValue("HairfaceStorage", out var savedHairface) ? savedHairface : "0/";
        if (baseline.Encoding.GetByteCount(hairfaceRaw) > 256 * 1024)
        { reason = "capturedPrefixHairfaceTooLong"; return false; }
        string hairface = hairfaceRaw.Trim();
        if (!TryBool(fields, "RelicSpirit", false, out bool relic) ||
            !TryBool(fields, "SpawinEffect", false, out bool spawn) ||
            !TryBool(fields, "SpawinEffectSkills", false, out bool spawnSkills) ||
            !TryBool(fields, "TailedBeastActivated", false, out beastActivated) ||
            !TryUInt(fields, "DeityLandLuckyPoints", out uint lucky) ||
            !TryUInt(fields, "HeroPoints", out uint hero) || !TryUInt(fields, "ClassExperience", out uint classExperience) ||
            !TryUInt(fields, "SavePromote", out uint promote) || !TryUInt(fields, "TailedBeastTotalPoints", out beastTotalPoints) ||
            !TryUInt(fields, "TailedBeastFlag", out beastFlag) ||
            !TryByte(fields, "SkinUnlock", out byte skin) || !TryByte(fields, "DoomosaurSkin", out byte doomosaur) ||
            !TryByte(fields, "PrayingElfSkin", out byte elf) || !TryByte(fields, "PolarDragonSkin", out byte dragon) ||
            !TryByte(fields, "ChargeChi", out byte chargeChi) || !TryByte(fields, "ChargeGuildSkill", out byte chargeGuild))
        { reason = "capturedPrefixOptionalValueInvalid"; return false; }
        var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out _);
        var parsed = CharacterSnapshotParser.ParseText(text);
        LegacyOrdinaryAdmissionValidation.ValidateCharacter(parsed);
        if (image.AccountId <= 0 || parsed.Uid != image.CharacterUid || parsed.Body is < 0 or > ushort.MaxValue ||
            parsed.Face is < 0 or > ushort.MaxValue || parsed.Online is < 0 or > uint.MaxValue)
        { reason = "capturedPrefixRequiredValueInvalid"; return false; }
        snapshot = parsed;
        values = new(checked((uint)parsed.Uid), checked((uint)parsed.Class), checked((ushort)parsed.Level), relic,
            checked((uint)parsed.Online), spawn, spawnSkills, lucky, hero, classExperience, promote,
            skin, doomosaur, elf, dragon, chargeChi, chargeGuild, checked((ushort)parsed.Body),
            checked((ushort)parsed.Face), name, spouse);
        hairfaceLine = hairface;
        reason = string.Empty;
        return true;
    }

    private static bool TryBool(IReadOnlyDictionary<string, string> fields, string key, bool fallback, out bool value)
    {
        value = fallback;
        return !fields.TryGetValue(key, out var raw) || bool.TryParse(raw.Trim(), out value);
    }

    private static bool TryUInt(IReadOnlyDictionary<string, string> fields, string key, out uint value)
    {
        value = 0;
        return !fields.TryGetValue(key, out var raw) || uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryByte(IReadOnlyDictionary<string, string> fields, string key, out byte value)
    {
        value = 0;
        return !fields.TryGetValue(key, out var raw) || byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
