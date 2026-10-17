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
    private sealed record CaptureTheFlagProjection(
        uint Uid,
        ushort X,
        ushort Y,
        bool Viable);

    private sealed record CaptureTheFlagPickupResult(
        uint FlagUid,
        ushort X,
        ushort Y,
        uint Exploits);

    private sealed record CaptureTheFlagBaseScore(
        uint BaseUid,
        ulong GuildId,
        string GuildName,
        uint Score);


    private const int LegacyCaptureTheFlagCarrierFlag = 0x76;
    private const long LegacyCaptureTheFlagMapId = 2_057;
    private const uint LegacyCaptureTheFlagCarrySeconds = 60;
    private const uint LegacyCaptureTheFlagPickupExploits = 3;

    [PacketHandler(WorldPacketOpcodes.LegacyCaptureTheFlagUpdate)]
    private static async ValueTask HandleLegacyCaptureTheFlagUpdateInboundAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyCaptureTheFlagUpdatePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyCaptureTheFlagUpdate packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyCaptureTheFlagUpdateMode.InitializeRank)
        {
            var response = LegacyCaptureTheFlagUpdatePacket.InitializeRank(isAlive: false);
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyCaptureTheFlagUpdate InitializeRank " +
                    $"{FormatSession(context)} active=0; " +
                    "source MsgCaptureTheFlagUpdate.InitializeRank inactive CaptureTheFlag state modeled.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Mode == LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect)
        {
            await HandleLegacyCaptureTheFlagPlantFlagAsync(context, packet, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        const string reason = "source ignores this inbound mode";
        await context.Log.WriteLineAsync(
                "WorldServer deferred LegacyCaptureTheFlagUpdate " +
                $"{FormatSession(context)} mode={packet.Mode} parameter={packet.Parameter} " +
                $"parameter2={packet.Parameter2} count={packet.Count}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyCaptureTheFlagPlantFlagAsync(
        WorldPacketHandlerContext context,
        LegacyCaptureTheFlagUpdatePacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCaptureTheFlagUpdate PlantFlag " +
                    $"{FormatSession(context)} mode={packet.Mode} parameter={packet.Parameter} " +
                    $"parameter2={packet.Parameter2} count={packet.Count}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryApplyCaptureTheFlagPlant(
                session.Character,
                out var updatedCharacter,
                out var plant))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyCaptureTheFlagUpdate PlantFlag " +
                    $"{FormatSession(context)} mode={packet.Mode} parameter={packet.Parameter} " +
                    $"parameter2={packet.Parameter2} count={packet.Count}; " +
                    "source MsgCaptureTheFlag.PlantTheFlag conditions were not met.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));

        await context.Connection.SendAsync(
                LegacyCaptureTheFlagRankingPacket.UpdatePoint(plant.Exploits).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagUpdatePacket.GenerateTimer(seconds: 0, uid).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagUpdatePacket.GenerateEffect(uid).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagUpdatePacket.RemoveFlagEffect(uid).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyCaptureTheFlagUpdate PlantFlag " +
                $"{FormatSession(updatedContext)} baseUid={plant.BaseUid} award={plant.Award} " +
                $"x2={(plant.X2 ? 1 : 0)} exploits={plant.Exploits}; " +
                "source MsgCaptureTheFlag.PlantTheFlag modeled.")
            .ConfigureAwait(false);
    }

    private static bool TryApplyCaptureTheFlagPlant(
        CharacterSnapshot character,
        out CharacterSnapshot updatedCharacter,
        out (uint BaseUid, uint Exploits, uint Award, bool X2) plant)
    {
        const uint plantExploits = 10;
        const long baseRadius = 11;

        updatedCharacter = character;
        plant = default;

        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (character.MapId != LegacyCaptureTheFlagMapId ||
            ReadAttributeUInt64(character.Attributes, "DynamicID", "DynamicId") != 0 ||
            guildId == 0 ||
            !HasLegacyStatusFlag(character, LegacyCaptureTheFlagCarrierFlag))
        {
            return false;
        }

        var bases = ReadCaptureTheFlagBases(character);
        foreach (var bas in bases)
        {
            if (!bas.Viable ||
                bas.CapturerGuildId != guildId ||
                LegacyGetDistance(character.X, character.Y, bas.X, bas.Y) > baseRadius)
            {
                continue;
            }

            var award = plantExploits;
            if (bas.X2)
            {
                award *= 2;
            }

            var currentExploits = ReadAttributeUInt64(
                character.Attributes,
                "CaptureTheFlagExploits",
                "CtfExploits",
                "CTF_Exploits");
            if (currentExploits > uint.MaxValue - award)
            {
                return false;
            }

            var updatedExploits = (uint)currentExploits + award;
            var updatedGuildExploits = Math.Min(
                uint.MaxValue,
                ReadAttributeUInt64(
                    character.Attributes,
                    "CaptureTheFlagGuildExploits",
                    "CtfGuildExploits",
                    "CTF_GuildExploits") + award);

            updatedCharacter = WithoutLegacyStatusFlag(character, LegacyCaptureTheFlagCarrierFlag);
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "CaptureTheFlagExploits", updatedExploits);
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "CaptureTheFlagGuildExploits", updatedGuildExploits);
            plant = (bas.Uid, updatedExploits, award, bas.X2);
            return true;
        }

        return false;
    }


    private static List<(uint Uid, ushort X, ushort Y, ulong CapturerGuildId, bool X2, bool Viable)>
        ReadCaptureTheFlagBases(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "CaptureTheFlagBases", "CtfBases", "CTFBases");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bases = new List<(uint Uid, ushort X, ushort Y, ulong CapturerGuildId, bool X2, bool Viable)>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 5 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !ulong.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var capturerGuildId) ||
                !TryParseLegacyBool(fields[4], out var x2))
            {
                continue;
            }

            var viable = fields.Length < 6 || TryParseLegacyBool(fields[5], out var parsedViable) && parsedViable;
            bases.Add((uid, x, y, capturerGuildId, x2, viable));
        }

        return bases;
    }

    private static bool TryApplyLegacyCaptureTheFlagBaseCapture(
        WorldSession session,
        SobNpc sobNpc,
        out WorldSession updatedSession,
        out ulong capturerGuildId,
        out string capturerGuildName)
    {
        updatedSession = session;
        capturerGuildId = 0;
        capturerGuildName = string.Empty;
        if (session.Character.MapId != LegacyCaptureTheFlagMapId ||
            sobNpc.Id < 0)
        {
            return false;
        }

        capturerGuildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (capturerGuildId == 0)
        {
            return false;
        }

        capturerGuildName = ReadAttributeString(session.Character.Attributes, "GuildName").Trim();
        if (capturerGuildName.Length == 0 &&
            capturerGuildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)capturerGuildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            capturerGuildName = guild.Name.Trim();
        }

        if (capturerGuildName.Length == 0)
        {
            return false;
        }

        var bases = ReadCaptureTheFlagBases(session.Character);
        for (var index = 0; index < bases.Count; index++)
        {
            var bas = bases[index];
            if (bas.Uid != (uint)sobNpc.Id ||
                !bas.Viable ||
                bas.CapturerGuildId == capturerGuildId)
            {
                continue;
            }

            if (TryGetCaptureTheFlagBaseScoreWinner(
                    session.Character,
                    bas.Uid,
                    out var scoreWinnerGuildId,
                    out var scoreWinnerGuildName))
            {
                capturerGuildId = scoreWinnerGuildId;
                capturerGuildName = scoreWinnerGuildName;
            }

            bases[index] = bas with { CapturerGuildId = capturerGuildId };
            var updatedCharacter = WithCaptureTheFlagBasesAttribute(session.Character, bases);
            updatedCharacter = WithoutCaptureTheFlagBaseScoresAttribute(updatedCharacter, bas.Uid);
            updatedSession = session with { Character = updatedCharacter };
            return true;
        }

        return false;
    }

    private static bool TryApplyCaptureTheFlagBaseScore(
        WorldSession session,
        SobNpc sobNpc,
        uint damageApplied,
        out WorldSession updatedSession,
        out ulong guildId,
        out string guildName,
        out uint score)
    {
        updatedSession = session;
        guildId = 0;
        guildName = string.Empty;
        score = 0;
        if (damageApplied == 0 ||
            session.Character.MapId != LegacyCaptureTheFlagMapId ||
            ReadAttributeUInt64(session.Character.Attributes, "DynamicID", "DynamicId") != 0 ||
            sobNpc.Id < 0)
        {
            return false;
        }

        guildId = ReadAttributeUInt64(session.Character.Attributes, "GuildID", "GuildId");
        if (guildId == 0)
        {
            return false;
        }

        guildName = ResolveLegacyCaptureTheFlagGuildName(session, guildId);
        if (guildName.Length == 0)
        {
            return false;
        }

        var baseUid = (uint)sobNpc.Id;
        var bases = ReadCaptureTheFlagBases(session.Character);
        if (!bases.Any(bas => bas.Uid == baseUid && bas.Viable))
        {
            return false;
        }

        var scores = ReadCaptureTheFlagBaseScores(session.Character);
        var attackerGuildId = guildId;
        var existingIndex = scores.FindIndex(entry => entry.BaseUid == baseUid && entry.GuildId == attackerGuildId);
        if (existingIndex >= 0)
        {
            var existing = scores[existingIndex];
            score = SaturatingAddUInt32(existing.Score, damageApplied);
            scores[existingIndex] = existing with
            {
                GuildName = guildName,
                Score = score,
            };
        }
        else
        {
            score = damageApplied;
            scores.Add(new CaptureTheFlagBaseScore(baseUid, guildId, guildName, score));
        }

        updatedSession = session with
        {
            Character = WithCaptureTheFlagBaseScoresAttribute(session.Character, scores),
        };
        return true;
    }

    private static string ResolveLegacyCaptureTheFlagGuildName(WorldSession session, ulong guildId)
    {
        var guildName = ReadAttributeString(session.Character.Attributes, "GuildName").Trim();
        if (guildName.Length == 0 &&
            guildId <= uint.MaxValue &&
            TryGetLegacyGuildInfo(session, (uint)guildId, out var guild) &&
            !string.IsNullOrWhiteSpace(guild.Name))
        {
            guildName = guild.Name.Trim();
        }

        return guildName;
    }

    private static bool TryGetCaptureTheFlagBaseScoreWinner(
        CharacterSnapshot character,
        uint baseUid,
        out ulong guildId,
        out string guildName)
    {
        var winner = ReadCaptureTheFlagBaseScores(character)
            .Where(entry => entry.BaseUid == baseUid)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.GuildId)
            .FirstOrDefault();
        guildId = winner?.GuildId ?? 0;
        guildName = winner?.GuildName ?? string.Empty;
        return winner is not null && guildId != 0 && guildName.Length != 0;
    }

    private static List<CaptureTheFlagBaseScore> ReadCaptureTheFlagBaseScores(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(
            character.Attributes,
            "CaptureTheFlagBaseScores",
            "CtfBaseScores",
            "CTFBaseScores");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scores = new List<CaptureTheFlagBaseScore>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(',', 4, StringSplitOptions.TrimEntries);
            if (fields.Length < 4 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var baseUid) ||
                !ulong.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var guildId) ||
                !uint.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score) ||
                guildId == 0)
            {
                continue;
            }

            var guildName = Uri.UnescapeDataString(fields[3]).Trim();
            if (guildName.Length == 0)
            {
                continue;
            }

            scores.Add(new CaptureTheFlagBaseScore(baseUid, guildId, guildName, score));
        }

        return scores;
    }

    private static CharacterSnapshot WithCaptureTheFlagBaseScoresAttribute(
        CharacterSnapshot character,
        IReadOnlyList<CaptureTheFlagBaseScore> scores)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        RemoveCaptureTheFlagBaseScoreAttributes(attributes);
        if (scores.Count > 0)
        {
            attributes["CaptureTheFlagBaseScores"] = string.Join(
                ';',
                scores
                    .OrderBy(entry => entry.BaseUid)
                    .ThenBy(entry => entry.GuildId)
                    .Select(entry => string.Join(
                        ',',
                        entry.BaseUid.ToString(CultureInfo.InvariantCulture),
                        entry.GuildId.ToString(CultureInfo.InvariantCulture),
                        entry.Score.ToString(CultureInfo.InvariantCulture),
                        Uri.EscapeDataString(entry.GuildName))));
        }

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithoutCaptureTheFlagBaseScoresAttribute(
        CharacterSnapshot character,
        uint baseUid)
    {
        var scores = ReadCaptureTheFlagBaseScores(character)
            .Where(entry => entry.BaseUid != baseUid)
            .ToArray();
        return WithCaptureTheFlagBaseScoresAttribute(character, scores);
    }

    private static void RemoveCaptureTheFlagBaseScoreAttributes(IDictionary<string, string> attributes)
    {
        attributes.Remove("CaptureTheFlagBaseScores");
        attributes.Remove("CtfBaseScores");
        attributes.Remove("CTFBaseScores");
    }

    private static uint SaturatingAddUInt32(uint left, uint right)
    {
        return left > uint.MaxValue - right ? uint.MaxValue : left + right;
    }

    private static CharacterSnapshot WithCaptureTheFlagBasesAttribute(
        CharacterSnapshot character,
        IReadOnlyList<(uint Uid, ushort X, ushort Y, ulong CapturerGuildId, bool X2, bool Viable)> bases)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("CaptureTheFlagBases");
        attributes.Remove("CtfBases");
        attributes.Remove("CTFBases");
        if (bases.Count > 0)
        {
            attributes["CaptureTheFlagBases"] = string.Join(
                ';',
                bases.Select(bas => string.Join(
                    ',',
                    bas.Uid.ToString(CultureInfo.InvariantCulture),
                    bas.X.ToString(CultureInfo.InvariantCulture),
                    bas.Y.ToString(CultureInfo.InvariantCulture),
                    bas.CapturerGuildId.ToString(CultureInfo.InvariantCulture),
                    bas.X2 ? "1" : "0",
                    bas.Viable ? "1" : "0")));
        }

        return character with { Attributes = attributes };
    }


    private static async ValueTask SendCaptureTheFlagPickupAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CaptureTheFlagPickupResult pickup,
        CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagUpdatePacket.GenerateTimer(LegacyCaptureTheFlagCarrySeconds, uid).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagRankingPacket.UpdatePoint(pickup.Exploits).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyCaptureTheFlagUpdatePacket.GenerateEffect(uid).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var action = new MsgActionPacket
        {
            ObjId = pickup.FlagUid,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.RemoveEntity,
            Facing = ReadAttributeUInt16(session.Character.Attributes, "Angle"),
            PositionX = pickup.X,
            PositionY = pickup.Y,
        };
        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent MsgMovement CaptureTheFlag flag pickup " +
                $"{FormatSession(context)} flagUid={pickup.FlagUid} x={pickup.X} y={pickup.Y} " +
                $"timer={LegacyCaptureTheFlagCarrySeconds} exploits={pickup.Exploits}.")
            .ConfigureAwait(false);
    }


    private static bool TryApplyCaptureTheFlagPickup(
        CharacterSnapshot character,
        out CharacterSnapshot updatedCharacter,
        out CaptureTheFlagPickupResult? pickup)
    {
        updatedCharacter = character;
        pickup = null;

        if (character.MapId != LegacyCaptureTheFlagMapId ||
            ReadAttributeUInt64(character.Attributes, "DynamicID", "DynamicId") != 0 ||
            ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId") == 0 ||
            HasLegacyStatusFlag(character, LegacyCaptureTheFlagCarrierFlag))
        {
            return false;
        }

        var flags = ReadCaptureTheFlagFlags(character);
        if (flags.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < flags.Count; index++)
        {
            var flag = flags[index];
            if (!flag.Viable ||
                LegacyGetDistance(character.X, character.Y, flag.X, flag.Y) >= 2)
            {
                continue;
            }

            var currentExploits = ReadAttributeUInt64(
                character.Attributes,
                "CaptureTheFlagExploits",
                "CtfExploits",
                "CTF_Exploits");
            if (currentExploits > uint.MaxValue - LegacyCaptureTheFlagPickupExploits)
            {
                return false;
            }

            var updatedExploits = (uint)currentExploits + LegacyCaptureTheFlagPickupExploits;
            var remainingFlags = flags
                .Where((_, flagIndex) => flagIndex != index)
                .ToArray();

            updatedCharacter = WithLegacyStatusFlag(character, LegacyCaptureTheFlagCarrierFlag);
            updatedCharacter = WithCharacterAttribute(updatedCharacter, "CaptureTheFlagExploits", updatedExploits);
            updatedCharacter = WithCaptureTheFlagFlagsAttribute(updatedCharacter, remainingFlags);
            pickup = new CaptureTheFlagPickupResult(flag.Uid, flag.X, flag.Y, updatedExploits);
            return true;
        }

        return false;
    }


    private static List<CaptureTheFlagProjection> ReadCaptureTheFlagFlags(CharacterSnapshot character)
    {
        var raw = ReadAttributeString(character.Attributes, "CaptureTheFlagFlags", "CtfFlags", "CTFFlags");
        if (raw.Length == 0)
        {
            return [];
        }

        var entries = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var flags = new List<CaptureTheFlagProjection>(entries.Length);
        foreach (var entry in entries)
        {
            var fields = entry.Split(
                [',', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length < 4 ||
                !uint.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !ushort.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !TryParseLegacyBool(fields[3], out var viable))
            {
                continue;
            }

            flags.Add(new CaptureTheFlagProjection(uid, x, y, viable));
        }

        return flags;
    }


    private static CharacterSnapshot WithCaptureTheFlagFlagsAttribute(
        CharacterSnapshot character,
        IReadOnlyList<CaptureTheFlagProjection> flags)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("CaptureTheFlagFlags");
        attributes.Remove("CtfFlags");
        attributes.Remove("CTFFlags");
        if (flags.Count > 0)
        {
            attributes["CaptureTheFlagFlags"] = WriteCaptureTheFlagFlags(flags);
        }

        return character with { Attributes = attributes };
    }

    private static string WriteCaptureTheFlagFlags(IReadOnlyList<CaptureTheFlagProjection> flags)
    {
        return string.Join(
            ';',
            flags.Select(flag => string.Join(
                ',',
                flag.Uid.ToString(CultureInfo.InvariantCulture),
                flag.X.ToString(CultureInfo.InvariantCulture),
                flag.Y.ToString(CultureInfo.InvariantCulture),
                flag.Viable ? "1" : "0")));
    }

}
