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
    [PacketHandler(WorldPacketOpcodes.LegacyLeagueOpt)]
    private static async ValueTask HandleLegacyLeagueOptAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyLeagueOptPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyLeagueOpt packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyLeagueOptPacket.ListAction)
        {
            await HandleLegacyLeagueOptListAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyLeagueOpt " +
                $"{FormatSession(context)} action={packet.Action} id={packet.Id} uid={packet.Uid} index={packet.Index} texts={packet.Texts.Count}; league state is not modeled.")
            .ConfigureAwait(false);
    }

    private const int LegacyLeagueAllegiancePageSize = 3;

    private static async ValueTask HandleLegacyLeagueOptListAsync(
        WorldPacketHandlerContext context,
        LegacyLeagueOptPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyLeagueOpt List " +
                    $"{FormatSession(context)} index={packet.Index}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeUInt64(context.Session.Character.Attributes, "UnionUID", "UnionID") != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyLeagueOpt List " +
                    $"{FormatSession(context)} index={packet.Index}; character is already in a union.")
                .ConfigureAwait(false);
            return;
        }

        var hasSource = TryLoadLegacyLeagueAllegianceEntries(
            context.Session.UsersRoot,
            out var allegianceEntries);
        var pageStart = GetLegacyLeagueAllegiancePageStart(packet.Index);
        var pageEntries = pageStart >= allegianceEntries.Count
            ? []
            : allegianceEntries.Skip(pageStart).Take(LegacyLeagueAllegiancePageSize).ToArray();
        var response = LegacyLeagueAllegianceListPacket.Create(
            checked((uint)allegianceEntries.Count),
            (ushort)packet.Index,
            pageEntries);

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyLeagueOpt List " +
                $"{FormatSession(context)} page={response.Page} total={response.Count} entries={response.PageCount} source={(hasSource ? "Unions" : "none")}.")
            .ConfigureAwait(false);
    }

    private static int GetLegacyLeagueAllegiancePageStart(uint page)
    {
        return page > int.MaxValue / LegacyLeagueAllegiancePageSize
            ? int.MaxValue
            : checked((int)page * LegacyLeagueAllegiancePageSize);
    }

    private static bool TryLoadLegacyLeagueAllegianceEntries(
        string? usersRoot,
        out IReadOnlyList<LegacyLeagueAllegianceEntry> entries)
    {
        entries = [];
        var unionsRoot = TryDeriveLegacyUnionsRoot(usersRoot);
        if (unionsRoot is null || !Directory.Exists(unionsRoot))
        {
            return false;
        }

        var parsedEntries = new List<LegacyLeagueAllegianceEntry>();
        foreach (var path in Directory.EnumerateFiles(unionsRoot, "*.ini"))
        {
            if (TryReadLegacyLeagueAllegianceEntry(path, out var entry))
            {
                parsedEntries.Add(entry);
            }
        }

        entries = parsedEntries
            .OrderByDescending(entry => entry.GoldBricks)
            .ThenBy(entry => entry.Uid)
            .ToArray();
        return true;
    }

    private static bool TryReadLegacyLeagueAllegianceEntry(
        string path,
        out LegacyLeagueAllegianceEntry entry)
    {
        entry = default;
        try
        {
            var document = IniDocument.Parse(
                LegacyCharacterSnapshotEncoding.GetString(File.ReadAllBytes(path)));
            var info = document.GetSection("Info");
            var uid = ReadAttributeUInt32(info, "UID");
            if (uid == 0)
            {
                return false;
            }

            entry = new LegacyLeagueAllegianceEntry(
                ReadAttributeUInt64(info, "Treasury"),
                uid,
                ReadAttributeUInt32(info, "GoldBrick"),
                ReadLegacyLeagueInfoString(info, "Name"),
                ReadLegacyLeagueInfoString(info, "Emperor"),
                ReadLegacyLeagueInfoString(info, "RecruitDeclaration"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static string ReadLegacyLeagueInfoString(
        IReadOnlyDictionary<string, string> info,
        string key)
    {
        var value = ReadAttributeString(info, key);
        return value.Length == 0 ? "None" : value;
    }

    private static string? TryDeriveLegacyUnionsRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        if (!string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dbRoot = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(dbRoot)
            ? null
            : Path.Combine(dbRoot, "Unions");
    }

}
