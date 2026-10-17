using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryPersistLegacyGuildProcessBulletinCharacterAsync(
            WorldPacketHandlerContext context,
            WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildBulletin"] = ReadAttributeString(session.Character.Attributes, "GuildBulletin", "Bulletin"),
            ["GuildBulletinEnroll"] = ReadAttributeUInt64(session.Character.Attributes, "GuildBulletinEnroll", "GuildBulletinEnrole", "BuletinEnrole")
                .ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Bulletin character persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess Bulletin character " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys=GuildBulletin,GuildBulletinEnroll.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyGuildProcessBulletinGuildAsync(
            WorldPacketHandlerContext context,
            LegacyGuildInfo guild)
    {
        if (!TryPersistLegacyGuildBulletin(
                context.Session?.UsersRoot,
                guild.GuildId,
                guild.Bulletin,
                guild.BulletinEnroll,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Bulletin guild persistence " +
                    $"{FormatSession(context)} guildId={guild.GuildId} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess Bulletin guild " +
                $"{FormatSession(context)} guildId={guild.GuildId} path={Path.GetFileName(path)} " +
                "fields=Bulletin,BuletinEnrole.")
            .ConfigureAwait(false);
    }

    private static bool TryPersistLegacyGuildBulletin(
            string? usersRoot,
            uint guildId,
            string bulletin,
            uint bulletinEnroll,
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

        var guildsRoot = TryDeriveLegacyGuildsRoot(usersRoot);
        if (guildsRoot is null)
        {
            reason = "guildsRootMissing";
            return false;
        }

        path = Path.Combine(guildsRoot, guildId.ToString(CultureInfo.InvariantCulture) + ".txt");
        if (!File.Exists(path))
        {
            reason = "guildFileMissing";
            return false;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            reason = "guildFileReadFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "guildFileReadDenied";
            return false;
        }

        var updated = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var parts = line.Split('/');
            if (parts.Length < 4 ||
                !uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGuildId) ||
                parsedGuildId != guildId)
            {
                continue;
            }

            var fields = parts.ToList();
            while (fields.Count <= 8)
            {
                fields.Add(string.Empty);
            }

            fields[7] = bulletin;
            fields[8] = bulletinEnroll.ToString(CultureInfo.InvariantCulture);
            var updatedLine = string.Join('/', fields);
            lines[index] = updatedLine.EndsWith("/", StringComparison.Ordinal)
                ? updatedLine
                : updatedLine + "/";
            updated = true;
            break;
        }

        if (!updated)
        {
            reason = "guildRecordMissing";
            return false;
        }

        try
        {
            File.WriteAllLines(path, lines, Encoding.UTF8);
            return true;
        }
        catch (IOException)
        {
            reason = "guildFileWriteFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "guildFileWriteDenied";
            return false;
        }
    }

    private static bool TryGetLegacyGuildInfo(WorldSession session, uint guildId, out LegacyGuildInfo guild)
    {
        guild = default!;
        if (TryGetLegacyGuildInfoFromAttributes(session.Character, guildId, out guild))
        {
            guild = WithLegacyGuildMemberAttributes(session.Character, guild);
            return true;
        }

        if (TryGetLegacyGuildInfoFromFile(session.UsersRoot, guildId, out guild))
        {
            guild = WithLegacyGuildMemberAttributes(session.Character, guild);
            return true;
        }

        return false;
    }

    private static bool TryGetLegacyGuildInfoFromAttributes(
            CharacterSnapshot character,
            uint guildId,
            out LegacyGuildInfo guild)
    {
        guild = default!;
        if (ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId") != guildId)
        {
            return false;
        }

        var name = ReadAttributeString(character.Attributes, "GuildName");
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var leader = ReadAttributeString(character.Attributes, "GuildLeaderName", "GuildLeader", "LeaderName");
        if (string.IsNullOrWhiteSpace(leader))
        {
            leader = character.Name;
        }

        guild = new LegacyGuildInfo(
            guildId,
            name,
            leader,
            (int)ReadAttributeUInt64(character.Attributes, "GuildLevel", "GuildLev"),
            (int)ReadAttributeUInt64(character.Attributes, "GuildMemberCount", "GuildMembers"),
            SafeReadAttributeInt64(character.Attributes, "GuildSilverFund", "SilverFund"),
            ReadAttributeUInt32(character.Attributes, "GuildConquerPointFund", "GuildCpFund", "ConquerPointFund"),
            ReadAttributeUInt32(character.Attributes, "GuildRank", "GuildMemberRank"),
            ReadAttributeUInt32(character.Attributes, "GuildCreateTime", "GuildCreationTime", "CreateTime"),
            ReadAttributeString(character.Attributes, "GuildBulletin", "Bulletin"),
            ReadAttributeUInt32(character.Attributes, "GuildBulletinEnroll", "GuildBulletinEnrole", "BuletinEnrole"),
            ReadAttributeUInt32(character.Attributes, "GuildLeaderId", "GuildLeaderID", "LeaderId"),
            ReadAttributeUInt64(character.Attributes, "GuildMaterial", "Material"),
            ReadAttributeUInt64(character.Attributes, "GuildPrestige", "Prestige"),
            ReadAttributeUInt32(character.Attributes, "GuildRecruitmentFlag", "RecruitmentFlag"),
            ReadAttributeUInt32(character.Attributes, "GuildRecruitmentBattlePower", "RecruitmentBattlePower"),
            ReadAttributeBool(character.Attributes, "GuildRecruitmentON", "GuildRecruitmentOn", "RecruitmentON"),
            ReadAttributeBool(character.Attributes, "GuildRecruitmentOFF", "GuildRecruitmentOff", "RecruitmentOFF"));
        return true;
    }

    private static bool TryGetLegacyGuildInfoFromFile(string? usersRoot, uint guildId, out LegacyGuildInfo guild)
    {
        guild = default!;
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return false;
        }

        var guildsRoot = TryDeriveLegacyGuildsRoot(usersRoot);
        if (guildsRoot is null)
        {
            return false;
        }

        var path = Path.Combine(guildsRoot, guildId.ToString(CultureInfo.InvariantCulture) + ".txt");
        if (!File.Exists(path))
        {
            return false;
        }

        var memberCount = 0;
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line["Count=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out memberCount);
                continue;
            }

            var parts = line.Split('/');
            if (parts.Length < 4 ||
                !uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedGuildId) ||
                parsedGuildId != guildId ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                continue;
            }

            guild = new LegacyGuildInfo(
                guildId,
                parts[1],
                parts[3],
                level,
                memberCount,
                TryReadLegacyGuildInt64Part(parts, 4),
                TryReadLegacyGuildUInt32Part(parts, 5),
                0,
                TryReadLegacyGuildUInt32Part(parts, 6),
                TryReadLegacyGuildStringPart(parts, 7),
                TryReadLegacyGuildUInt32Part(parts, 8),
                TryReadLegacyGuildUInt32Part(parts, 10),
                TryReadLegacyGuildUInt64Part(parts, 11),
                TryReadLegacyGuildUInt64Part(parts, 12),
                TryReadLegacyGuildUInt32Part(parts, 13),
                TryReadLegacyGuildUInt32Part(parts, 14),
                TryReadLegacyGuildBoolPart(parts, 15),
                TryReadLegacyGuildBoolPart(parts, 16));
            return true;
        }

        return false;
    }

    private static LegacyGuildInfo WithLegacyGuildMemberAttributes(CharacterSnapshot character, LegacyGuildInfo guild)
    {
        if (ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId") != guild.GuildId)
        {
            return guild;
        }

        var rank = ReadAttributeUInt32(character.Attributes, "GuildRank", "GuildMemberRank");
        if (rank == 0)
        {
            rank = string.Equals(character.Name, guild.LeaderName, StringComparison.OrdinalIgnoreCase)
                ? 1U
                : 6U;
        }

        return guild with { MyRank = rank };
    }

    private static bool ShouldSendLegacyGuildBulletin(string bulletin)
    {
        return bulletin.Length != 0 &&
            !string.Equals(bulletin, "None", StringComparison.Ordinal);
    }

    private static uint GetLegacyGuildBulletinEnroll()
    {
        var now = DateTime.Now;
        return (uint)((now.Year * 10_000) + (now.Month * 100) + now.Day);
    }

    private static bool IsLegacyGuildBulletinAllowed(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (LegacyNameInvalidTrailingCharacterPattern.IsMatch(text) ||
            text.IndexOf('/') >= 0 ||
            text.IndexOf('\\') >= 0 ||
            text.IndexOf('\'') >= 0)
        {
            return false;
        }

        foreach (var forbidden in LegacyNameForbiddenTokens)
        {
            if (text.Contains(forbidden, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return text.IndexOf('\u0019') < 0;
    }

    private static ushort ClampToUInt16(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value >= ushort.MaxValue
            ? ushort.MaxValue
            : (ushort)value;
    }

    private static long SafeReadAttributeInt64(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static string TryReadLegacyGuildStringPart(string[] parts, int index)
    {
        return index < parts.Length
            ? parts[index]
            : string.Empty;
    }

    private static long TryReadLegacyGuildInt64Part(string[] parts, int index)
    {
        return index < parts.Length &&
            long.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static uint TryReadLegacyGuildUInt32Part(string[] parts, int index)
    {
        return index < parts.Length &&
            uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static ulong TryReadLegacyGuildUInt64Part(string[] parts, int index)
    {
        return index < parts.Length &&
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static bool TryReadLegacyGuildBoolPart(string[] parts, int index)
    {
        return index < parts.Length &&
            (parts[index].Equals("true", StringComparison.OrdinalIgnoreCase) ||
                parts[index].Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                (ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != 0));
    }

    private static string? TryDeriveLegacyGuildsRoot(string usersRoot)
    {
        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (!string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dbRoot = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(dbRoot)
            ? null
            : Path.Combine(dbRoot, "Guilds");
    }
}
