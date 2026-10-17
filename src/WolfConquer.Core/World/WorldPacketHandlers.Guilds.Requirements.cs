using System.Globalization;
using System.Text;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyGuildProcessRequirementsAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyGuildProcessPacket packet,
            CancellationToken cancellationToken)
    {
        var character = session.Character;
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (guildId == 0 ||
            guildId > uint.MaxValue ||
            !TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) ||
            guild.MyRank != 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Requirements " +
                    $"{FormatSession(context)} uid={packet.Uid} guildId={guildId}; " +
                    "only guild leaders can update recruitment settings.")
                .ConfigureAwait(false);
            return;
        }

        var recruitmentBattlePower = unchecked((uint)packet.Argument1);
        var recruitmentFlag = unchecked((uint)packet.Argument2);
        var recruitArgs = unchecked((uint)packet.Argument3);
        var recruitmentOff = (recruitArgs & 0xFFu) == 1;
        var recruitmentOn = ((recruitArgs >> 8) & 0xFFu) == 1;

        var updatedGuild = guild with
        {
            RecruitmentFlag = recruitmentFlag,
            RecruitmentBattlePower = recruitmentBattlePower,
            RecruitmentOn = recruitmentOn,
            RecruitmentOff = recruitmentOff,
        };

        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithCharacterAttribute(
                        character,
                        "GuildRecruitmentFlag",
                        recruitmentFlag),
                    "GuildRecruitmentBattlePower",
                    recruitmentBattlePower),
                "GuildRecruitmentON",
                recruitmentOn ? 1UL : 0UL),
            "GuildRecruitmentOFF",
            recruitmentOff ? 1UL : 0UL);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyGuildProcessRequirementsCharacterAsync(updatedContext, updatedSession)
            .ConfigureAwait(false);
        await TryPersistLegacyGuildProcessRequirementsGuildAsync(updatedContext, updatedGuild)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                "Requirements " +
                "action=Requirements " +
                $"{FormatSession(updatedContext)} uid={packet.Uid} guildId={guild.GuildId} " +
                $"flag={recruitmentFlag} battlePower={recruitmentBattlePower} " +
                $"recruitmentOn={recruitmentOn} recruitmentOff={recruitmentOff}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyGuildProcessRequirementsCharacterAsync(
            WorldPacketHandlerContext context,
            WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GuildRecruitmentFlag"] = ReadAttributeUInt64(
                session.Character.Attributes,
                "GuildRecruitmentFlag").ToString(CultureInfo.InvariantCulture),
            ["GuildRecruitmentBattlePower"] = ReadAttributeUInt64(
                session.Character.Attributes,
                "GuildRecruitmentBattlePower").ToString(CultureInfo.InvariantCulture),
            ["GuildRecruitmentON"] = ReadAttributeUInt64(
                session.Character.Attributes,
                "GuildRecruitmentON")
                    .ToString(CultureInfo.InvariantCulture),
            ["GuildRecruitmentOFF"] = ReadAttributeUInt64(
                session.Character.Attributes,
                "GuildRecruitmentOFF")
                    .ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Requirements character persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess Requirements character " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys=GuildRecruitmentFlag,"
                +
                "GuildRecruitmentBattlePower,GuildRecruitmentON,GuildRecruitmentOFF.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyGuildProcessRequirementsGuildAsync(
            WorldPacketHandlerContext context,
            LegacyGuildInfo guild)
    {
        if (!TryPersistLegacyGuildRequirements(context.Session?.UsersRoot, guild, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess Requirements guild persistence " +
                    $"{FormatSession(context)} guildId={guild.GuildId} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess Requirements guild " +
                $"{FormatSession(context)} guildId={guild.GuildId} path={Path.GetFileName(path)} " +
                "fields=RecruitmentFlag,RecruitmentBattlePower,RecruitmentOn,RecruitmentOff.")
            .ConfigureAwait(false);
    }

    private static bool TryPersistLegacyGuildRequirements(
            string? usersRoot,
            LegacyGuildInfo guild,
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

        path = Path.Combine(guildsRoot, guild.GuildId.ToString(CultureInfo.InvariantCulture) + ".txt");
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
            var parts = lines[index].Split('/');
            if (!uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId) ||
                guildId != guild.GuildId)
            {
                continue;
            }

            var fields = parts.ToList();
            while (fields.Count <= 16)
            {
                fields.Add(string.Empty);
            }

            fields[13] = guild.RecruitmentFlag.ToString(CultureInfo.InvariantCulture);
            fields[14] = guild.RecruitmentBattlePower.ToString(CultureInfo.InvariantCulture);
            fields[15] = guild.RecruitmentOn ? "1" : "0";
            fields[16] = guild.RecruitmentOff ? "1" : "0";

            lines[index] = string.Join('/', fields);
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
}
