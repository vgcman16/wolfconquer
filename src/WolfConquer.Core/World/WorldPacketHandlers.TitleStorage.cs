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

    private readonly record struct LegacyRuntimeTitleType(uint Id, uint SubId, uint Score);

    private const string LegacyTitleStorageSourceName = "CMsgTitleStorage";

    private const string LegacyTitleStorageSourceEvidence =
            "MrRayzo.Game.MsgServer.MsgTitleStorage defines [Packet(2313)] Process and CreateTitleStorage calls Finalize(2313); MrRayzo.Packets maps ID 2313 to CMsgTitleStorage.";

    [PacketHandler(WorldPacketOpcodes.LegacyMsgTitleStorage)]
    private static async ValueTask HandleLegacyMsgTitleStorageAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTitleStoragePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMsgTitleStorage packet {FormatSession(context)}. {FormatFrameDiagnostic(frame)}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMsgTitleStorage " +
                    $"{FormatSession(context)} action={packet.Action} dwParam1={packet.DwParam1} " +
                    $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3}; no character session is available. " +
                    $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is not (LegacyTitleStorageAction.Equip or LegacyTitleStorageAction.UnEquip))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyMsgTitleStorage " +
                    $"{FormatSession(context)} action={packet.Action} dwParam1={packet.DwParam1} " +
                    $"dwParam2={packet.DwParam2} dwParam3={packet.DwParam3}; " +
                    "legacy Process only mutates Equip and UnEquip actions. " +
                    $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacyRuntimeTitleType(context, session, packet.DwParam2, out var title))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMsgTitleStorage " +
                    $"{FormatSession(context)} action={packet.Action} titleId={packet.DwParam2}; " +
                    "title_type entry was not found. " +
                    $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacySpecialTitle(session.Character, packet.DwParam2))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMsgTitleStorage " +
                    $"{FormatSession(context)} action={packet.Action} titleId={packet.DwParam2} subId={packet.DwParam3}; " +
                    "title is not owned by the character. " +
                    $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
                .ConfigureAwait(false);
            return;
        }

        if (context.TradeRecovery is null || context.TradeRecovery.IsBlocked)
        {
            await LogLegacyTitleStorageCommitFailureAsync(context, "recoveryOwnerUnavailableOrBlocked").ConfigureAwait(false);
            return;
        }

        if (context.Sessions is null || session.AccountId <= 0 || session.Character.Uid != session.AccountId ||
            title.Id != packet.DwParam2 || !IsRepresentableLegacyRuntimeTitle(title))
        {
            await context.Log.WriteLineAsync("WorldServer skipped LegacyMsgTitleStorage; session store or valid title metadata unavailable.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.Title is { } nested && (nested.Id != title.Id || nested.SubId != title.SubId))
        {
            await context.Log.WriteLineAsync("WorldServer skipped LegacyMsgTitleStorage; nested title identity mismatch.")
                .ConfigureAwait(false);
            return;
        }

        // Do not trust a client-supplied subtype in a server-issued title response.
        packet = packet with { DwParam2 = title.Id, DwParam3 = title.SubId };
        if (packet.Action == LegacyTitleStorageAction.Equip)
        {
            await HandleLegacyMsgTitleStorageEquipAsync(context, session, packet, title, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleLegacyMsgTitleStorageUnEquipAsync(context, session, packet, title, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyMsgTitleStorageEquipAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyTitleStoragePacket packet,
            LegacyRuntimeTitleType title,
            CancellationToken cancellationToken)
    {
        var activeTitleValue = checked((ulong)title.Id * 10_000UL + title.SubId);
        var updatedCharacter = title.Id < 4_001
            ? WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "SpecialTitleScore", title.Score),
                "SpecialTitleID",
                activeTitleValue)
            : WithCharacterAttribute(session.Character, "SpecialWingID", activeTitleValue);
        updatedCharacter = WithLegacySpecialTitleActiveFlag(updatedCharacter, title.Id, active: true);
        updatedCharacter = CanonicalizeLegacyTitleNumericAttributes(updatedCharacter);
        var updatedSession = session with { Character = updatedCharacter };
        var updatedContext = context with { Session = updatedSession };
        var useTitle = packet with
        {
            Action = LegacyTitleStorageAction.UseTitle,
            Title = new LegacyTitleStorageTitle(title.Id, title.SubId, title.Id >= 4_001 ? 1U : title.Score),
        };
        var publication = PrepareLegacyTitleStoragePublication(context, updatedSession, packet, useTitle);
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitLegacyTitleStorage(context, session, updatedSession, publication, cancellationToken, out var reason))
        {
            await LogLegacyTitleStorageCommitFailureAsync(context, reason).ConfigureAwait(false);
            return;
        }
        await PublishLegacyTitleStorageAsync(updatedContext, publication, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMsgTitleStorage " +
                $"{FormatSession(updatedContext)} action={packet.Action} titleId={packet.DwParam2} " +
                $"subId={packet.DwParam3} activeValue={activeTitleValue} outcome=committed delivery=bestEffort. " +
                $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyMsgTitleStorageUnEquipAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyTitleStoragePacket packet,
            LegacyRuntimeTitleType title,
            CancellationToken cancellationToken)
    {
        var slot = IsLegacyTitleSlot(title.Id) ? "SpecialTitleID" : "SpecialWingID";
        var expectedActive = checked((ulong)title.Id * 10_000UL + title.SubId);
        if (ReadAttributeUInt64(session.Character.Attributes, slot) != expectedActive)
        {
            await LogLegacyTitleStorageCommitFailureAsync(context, "requestedTitleIsNotEquipped").ConfigureAwait(false);
            return;
        }
        var updatedCharacter = title.Id < 4_001
            ? WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "SpecialTitleScore", 0UL),
                "SpecialTitleID",
                0UL)
            : WithCharacterAttribute(session.Character, "SpecialWingID", 0UL);
        updatedCharacter = WithLegacySpecialTitleActiveFlag(updatedCharacter, title.Id, active: false);
        updatedCharacter = CanonicalizeLegacyTitleNumericAttributes(updatedCharacter);
        var updatedSession = session with { Character = updatedCharacter };
        var updatedContext = context with { Session = updatedSession };
        var publication = PrepareLegacyTitleStoragePublication(context, updatedSession, packet, useTitle: null);
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitLegacyTitleStorage(context, session, updatedSession, publication, cancellationToken, out var reason))
        {
            await LogLegacyTitleStorageCommitFailureAsync(context, reason).ConfigureAwait(false);
            return;
        }
        await PublishLegacyTitleStorageAsync(updatedContext, publication, cancellationToken).ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMsgTitleStorage " +
                $"{FormatSession(updatedContext)} action={packet.Action} titleId={packet.DwParam2} " +
                $"subId={packet.DwParam3} outcome=committed delivery=bestEffort. " +
                $"sourceName={LegacyTitleStorageSourceName} sourceEvidence={LegacyTitleStorageSourceEvidence}")
            .ConfigureAwait(false);
    }

    [PacketHandler(WorldPacketOpcodes.LegacyTitle)]
    private static async ValueTask HandleLegacyTitleAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyTitlePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyTitle packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyTitle {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var titles = ReadLegacyClassicTitles(session.Character);
        switch (packet.Action)
        {
            case LegacyTitleAction.List:
                foreach (var title in titles)
                {
                    await context.Connection.SendAsync(
                            new LegacyTitlePacket(uid, title, LegacyTitleAction.Enqueue).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await context.Log.WriteLineAsync(
                        $"WorldServer handled LegacyTitle list {FormatSession(context)} count={titles.Count}.")
                    .ConfigureAwait(false);
                break;

            case LegacyTitleAction.Change when packet.Title == 0 || titles.Contains(packet.Title):
                {
                    var updatedCharacter = WithCharacterAttribute(session.Character, "MyTitle", packet.Title);
                    var updatedSession = session with { Character = updatedCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    await context.Connection.SendAsync(
                            new LegacyTitlePacket(uid, packet.Title, LegacyTitleAction.Change).ToFrameBytes(),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await context.Log.WriteLineAsync(
                            $"WorldServer handled LegacyTitle change {FormatSession(context)} title={packet.Title}.")
                        .ConfigureAwait(false);
                    break;
                }

            case LegacyTitleAction.Change:
                await context.Log.WriteLineAsync(
                        $"WorldServer skipped LegacyTitle change {FormatSession(context)} title={packet.Title}; title is not owned.")
                    .ConfigureAwait(false);
                break;
        }
    }

    private static IReadOnlyList<byte> ReadLegacyClassicTitles(CharacterSnapshot character)
    {
        if (!character.Attributes.TryGetValue("Titles", out var rawTitles) ||
            string.IsNullOrWhiteSpace(rawTitles))
        {
            return [];
        }

        var titles = new List<byte>();
        var seen = new HashSet<byte>();
        foreach (var part in rawTitles.Split(
            [',', '/', ';', '|', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!byte.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var title) ||
                title == 0 ||
                !seen.Add(title))
            {
                continue;
            }

            titles.Add(title);
        }

        return titles;
    }

    private static bool TryFindLegacyRuntimeTitleType(
            WorldPacketHandlerContext context,
            WorldSession session,
            uint titleId,
            out LegacyRuntimeTitleType title)
    {
        title = default;

        if (titleId <= int.MaxValue &&
            context.WorldData?.TitleTypesById.TryGetValue((int)titleId, out var catalogTitle) == true &&
            catalogTitle.Id >= 0 &&
            catalogTitle.TypeId >= 0 &&
            catalogTitle.Score >= 0)
        {
            title = new LegacyRuntimeTitleType(
                (uint)catalogTitle.Id,
                (uint)catalogTitle.TypeId,
                (uint)catalogTitle.Score);
            return true;
        }

        var dbRoot = TryDeriveLegacyDbRoot(session.UsersRoot);
        if (dbRoot is null)
        {
            return false;
        }

        var titleTypePath = Path.Combine(dbRoot, "title_type.txt");
        if (!File.Exists(titleTypePath))
        {
            return false;
        }

        try
        {
            foreach (var line in File.ReadLines(titleTypePath))
            {
                if (TryReadLegacyRuntimeTitleTypeRow(line, titleId, out title))
                {
                    return true;
                }
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

        title = default;
        return false;
    }

    private static bool TryReadLegacyRuntimeTitleTypeRow(
            string line,
            uint titleId,
            out LegacyRuntimeTitleType title)
    {
        title = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Contains("@@", StringComparison.Ordinal)
            ? line.Split("@@", StringSplitOptions.None)
            : line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8 ||
            !uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ||
            id != titleId ||
            !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var subId) ||
            !uint.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
        {
            return false;
        }

        title = new LegacyRuntimeTitleType(id, subId, score);
        return true;
    }

    private static bool HasLegacySpecialTitle(CharacterSnapshot character, uint titleId)
    {
        if (!character.Attributes.TryGetValue("SpecialTitles", out var rawSpecialTitles) ||
            string.IsNullOrWhiteSpace(rawSpecialTitles))
        {
            return false;
        }

        var parts = rawSpecialTitles.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length > 0 && parts[^1].Length == 0) parts = parts[..^1];
        if (parts.Any(static part => part.Length == 0)) return false;
        if (!TryGetLegacySpecialTitlePairRange(parts, out var startIndex, out var endIndex))
        {
            return false;
        }

        var seen = new HashSet<uint>();
        var found = false;
        for (var index = startIndex; index + 1 < endIndex; index += 2)
        {
            if (!uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTitleId) ||
                parsedTitleId == 0 || !seen.Add(parsedTitleId) ||
                !byte.TryParse(parts[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag) || flag > 1)
                return false;
            found |= parsedTitleId == titleId;
        }

        return found;
    }

    private static CharacterSnapshot WithLegacySpecialTitleActiveFlag(
            CharacterSnapshot character,
            uint titleId,
            bool active)
    {
        if (!character.Attributes.TryGetValue("SpecialTitles", out var rawSpecialTitles) ||
            string.IsNullOrWhiteSpace(rawSpecialTitles))
        {
            return character;
        }

        var parts = rawSpecialTitles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!TryGetLegacySpecialTitlePairRange(parts, out var startIndex, out var endIndex))
        {
            return character;
        }

        var titleSlot = IsLegacyTitleSlot(titleId);
        var matched = false;
        var changed = false;
        for (var index = startIndex; index + 1 < endIndex; index += 2)
        {
            if (!uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ownedTitleId) ||
                IsLegacyTitleSlot(ownedTitleId) != titleSlot)
            {
                continue;
            }

            var nextFlag = active && ownedTitleId == titleId ? "1" : "0";
            matched |= ownedTitleId == titleId;
            changed |= !string.Equals(parts[index + 1], nextFlag, StringComparison.Ordinal);
            parts[index + 1] = nextFlag;
        }

        if (!matched || !changed)
        {
            return character;
        }

        return WithCharacterAttribute(character, "SpecialTitles", string.Join(",", parts) + ",");
    }

    private static bool IsLegacyTitleSlot(uint titleId)
    {
        return titleId < 4_001;
    }

    private static bool TryGetLegacySpecialTitlePairRange(
            string[] parts,
            out int startIndex,
            out int endIndex)
    {
        startIndex = 0;
        endIndex = parts.Length;

        if (parts.Length == 0)
        {
            return true;
        }

        if (uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var declaredCount) &&
            declaredCount <= int.MaxValue / 2)
        {
            var declaredPairFieldCount = checked((int)declaredCount * 2);
            if (declaredPairFieldCount == parts.Length - 1)
            {
                startIndex = 1;
                return true;
            }

            if (declaredCount == 0 && LegacySpecialTitleRemainingPartsAreZero(parts))
            {
                startIndex = 1;
                endIndex = 1;
                return true;
            }
        }

        return parts.Length % 2 == 0;
    }

    private static bool LegacySpecialTitleRemainingPartsAreZero(string[] parts)
    {
        for (var index = 1; index < parts.Length; index++)
        {
            if (!uint.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                value != 0)
            {
                return false;
            }
        }

        return true;
    }
}
