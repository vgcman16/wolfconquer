using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryPersistLegacyClanMembershipAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string action,
        IReadOnlyList<string> keys)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            values[key] = key switch
            {
                "Money" => session.Character.Money.ToString(CultureInfo.InvariantCulture),
                _ when session.Character.Attributes.TryGetValue(key, out var value) => value,
                _ => "0",
            };
        }

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyClan persistence " +
                    $"{FormatSession(context with { Session = session })} action={action} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyClan " +
                $"{FormatSession(context with { Session = session })} action={action} " +
                $"path={Path.GetFileName(path)} keys={string.Join(',', keys)}.")
            .ConfigureAwait(false);
    }

    private static bool TryFindLegacyClanMemberFile(
        string? usersRoot,
        uint clanId,
        string name,
        out LegacyClanMemberFile member)
    {
        member = default;
        if (string.IsNullOrWhiteSpace(usersRoot) || !Directory.Exists(usersRoot))
        {
            return false;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(usersRoot, "*.ini"))
            {
                if (!TryReadLegacyClanMemberFile(path, out var candidate) ||
                    !string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    ReadAttributeUInt64(candidate.Attributes, "ClanID", "ClanUID") != clanId)
                {
                    continue;
                }

                member = candidate;
                return true;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    private static bool TryFindActiveLegacyClanMemberSession(
        WorldSessionStore? sessions,
        uint clanId,
        long uid,
        string name,
        out WorldActiveSession activeSession)
    {
        activeSession = default!;
        if (sessions is null)
        {
            return false;
        }

        foreach (var active in sessions.Snapshot())
        {
            var activeClanId = ReadAttributeUInt64(active.Session.Character.Attributes, "ClanID", "ClanUID");
            if (activeClanId == clanId &&
                (active.Session.Character.Uid == uid ||
                 string.Equals(active.Session.Character.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                activeSession = active;
                return true;
            }
        }

        return false;
    }

    private static bool TryPersistLegacyClanLeaderName(
        string? usersRoot,
        uint clanId,
        string leaderName,
        out string path,
        out string reason)
    {
        path = string.Empty;
        reason = string.Empty;
        var dbRoot = TryDeriveLegacyDbRoot(usersRoot);
        if (dbRoot is null)
        {
            reason = "usersRootMissing";
            return false;
        }

        path = Path.Combine(dbRoot, "Clans", clanId.ToString(CultureInfo.InvariantCulture) + ".txt");
        if (!File.Exists(path))
        {
            reason = "clanFileMissing";
            return false;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException)
        {
            reason = "clanReadFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "clanReadDenied";
            return false;
        }

        var clanLineIndex = Array.FindIndex(lines, line => !line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase));
        if (clanLineIndex < 0)
        {
            reason = "clanLineMissing";
            return false;
        }

        var parts = lines[clanLineIndex].Split('/', StringSplitOptions.None);
        if (parts.Length < 7)
        {
            reason = "clanLineMalformed";
            return false;
        }

        parts[2] = leaderName;
        lines[clanLineIndex] = string.Join('/', parts);

        try
        {
            File.WriteAllLines(path, lines);
            return true;
        }
        catch (IOException)
        {
            reason = "clanWriteFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "clanWriteDenied";
            return false;
        }
    }

    private static bool TryReadLegacyClanMemberFile(string path, out LegacyClanMemberFile member)
    {
        member = default;
        try
        {
            var attributes = IniDocument.Parse(File.ReadAllText(path, LegacyCharacterSnapshotEncoding))
                .GetSection("Character");
            if (!attributes.TryGetValue("Name", out var name) ||
                !attributes.TryGetValue("UID", out var rawUid) ||
                !long.TryParse(rawUid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
            {
                return false;
            }

            member = new LegacyClanMemberFile(path, uid, name, attributes);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool TryPersistLegacyCharacterAttributes(
        string path,
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyList<string> keys,
        out string reason)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            values[key] = attributes.TryGetValue(key, out var value) ? value : "0";
        }

        return CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
            path,
            expectedCharacterUid: null,
            values: values,
            detailedIoFailures: false,
            reason: out reason);
    }

    private readonly record struct LegacyClanMemberFile(
        string Path,
        long Uid,
        string Name,
        IReadOnlyDictionary<string, string> Attributes);
}
