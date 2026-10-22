using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    [PacketHandler(WorldPacketOpcodes.LegacyItemRefineRecord)]
    private static async ValueTask HandleLegacyItemRefineRecordAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = LegacyItemRefineRecordPacket.DefaultResponse();
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyItemRefineRecord " +
                $"{FormatSession(context)} points={response.Points}.")
            .ConfigureAwait(false);
    }



    [PacketHandler(WorldPacketOpcodes.LegacyTrade)]
    private static async ValueTask HandleLegacyTradeAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        await HandleLegacyTradeRuntimeAsync(context, frame, cancellationToken).ConfigureAwait(false);
    }









    private static bool TryPersistLegacyCharacterAttributes(
        string? usersRoot,
        long accountId,
        IReadOnlyDictionary<string, string> values,
        out string path,
        out string reason)
    {
        return TryPersistLegacyCharacterAttributes(
            usersRoot,
            accountId,
            expectedCharacterUid: accountId,
            values,
            out path,
            out reason);
    }

    private static bool TryPersistLegacyCharacterAttributes(
        string? usersRoot,
        long accountId,
        long expectedCharacterUid,
        IReadOnlyDictionary<string, string> values,
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

        return CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
            path,
            expectedCharacterUid,
            values,
            detailedIoFailures: false,
            out reason);
    }

    private static bool TryValidateLegacyCharacterUid(
        IReadOnlyList<string> lines,
        long expectedCharacterUid,
        out string reason)
    {
        reason = string.Empty;
        var inCharacterSection = false;
        var foundCharacterSection = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var sectionName = trimmed[1..^1].Trim();
                inCharacterSection = string.Equals(sectionName, "Character", StringComparison.OrdinalIgnoreCase);
                foundCharacterSection |= inCharacterSection;
                continue;
            }

            if (!inCharacterSection ||
                trimmed.Length == 0 ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..equalsIndex].Trim();
            if (!string.Equals(key, "UID", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawUid = rawLine[(equalsIndex + 1)..].Trim();
            if (!long.TryParse(rawUid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
            {
                reason = "uidInvalid";
                return false;
            }

            if (uid != expectedCharacterUid)
            {
                reason = "uidMismatch";
                return false;
            }

            return true;
        }

        reason = foundCharacterSection ? "uidMissing" : "characterSectionMissing";
        return false;
    }

    private static bool TryApplyLegacyCharacterAttributeLines(
        string[] lines,
        IReadOnlyDictionary<string, string> values,
        out string[] updatedLines)
    {
        return TryApplyLegacySectionAttributeLines(
            lines,
            "Character",
            values,
            createIfMissing: false,
            out updatedLines);
    }

    private static bool TryApplyLegacySectionAttributeLines(
        string[] lines,
        string sectionName,
        IReadOnlyDictionary<string, string> values,
        bool createIfMissing,
        out string[] updatedLines)
    {
        updatedLines = lines;
        var inTargetSection = false;
        var foundTargetSection = false;
        var insertIndex = lines.Length;
        var remaining = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inTargetSection)
                {
                    insertIndex = index;
                }

                var currentSectionName = trimmed[1..^1].Trim();
                inTargetSection = string.Equals(currentSectionName, sectionName, StringComparison.OrdinalIgnoreCase);
                foundTargetSection |= inTargetSection;
                if (inTargetSection)
                {
                    insertIndex = index + 1;
                }

                continue;
            }

            if (!inTargetSection)
            {
                continue;
            }

            insertIndex = index + 1;
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            var key = rawLine[..equalsIndex].Trim();
            if (!remaining.Remove(key, out var value))
            {
                continue;
            }

            lines[index] = rawLine[..(equalsIndex + 1)] + value;
        }

        if (!foundTargetSection)
        {
            if (!createIfMissing)
            {
                return false;
            }

            var appended = new List<string>(lines.Length + remaining.Count + 2);
            appended.AddRange(lines);
            if (appended.Count > 0 && appended[^1].Length != 0)
            {
                appended.Add(string.Empty);
            }

            appended.Add("[" + sectionName + "]");
            appended.AddRange(remaining.Select(pair => pair.Key + "=" + pair.Value));
            updatedLines = appended.ToArray();
            return true;
        }

        if (remaining.Count == 0)
        {
            return true;
        }

        var expanded = lines
            .Take(insertIndex)
            .Concat(remaining.Select(pair => pair.Key + "=" + pair.Value))
            .Concat(lines.Skip(insertIndex))
            .ToArray();
        updatedLines = expanded;
        return true;
    }

}
