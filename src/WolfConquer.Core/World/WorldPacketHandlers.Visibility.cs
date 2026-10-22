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
    private static async ValueTask SendVisibleStaticNpcObjectsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        await SendVisibleStaticNpcHeadObjectsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
        await SendVisibleSobNpcTailObjectsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendVisibleStaticNpcHeadObjectsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        await SendVisibleStaticNpcsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
        await SendVisibleChristmasNpcsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
        await SendVisibleFurnitureNpcsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendVisibleSobNpcTailObjectsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        await SendVisibleSobNpcsAsync(context, character, cancellationToken, previousCharacter)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendVisibleStaticNpcsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        var sendOnlyNewlyVisible = previousCharacter is not null;
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacyNpcSpawn visibility " +
                        $"{FormatSession(context)}; map id is out of Int32 range.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (context.WorldData is not { } worldData)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacyNpcSpawn visibility " +
                        $"{FormatSession(context)}; world data is not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!worldData.NpcsByMapId.TryGetValue((int)character.MapId, out var npcs))
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacyNpcSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)}; " +
                            "source LegacyInstance.Info dynamic map uses custom instance-scoped spawns.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP no LegacyNpcSpawn rows loaded " +
                            $"{FormatSession(context)} {FormatStaticNpcVisibilityDiagnostic(character, "no-rows-loaded")}.")
                        .ConfigureAwait(false);
                }
            }

            return;
        }

        var sentCount = 0;
        foreach (var npc in npcs)
        {
            if (!CanSeeStaticNpc(character, npc))
            {
                continue;
            }

            if (previousCharacter is { } previous &&
                CanSeeStaticNpc(previous, npc))
            {
                continue;
            }

            var spawn = LegacyNpcSpawnPacket.FromNpc(npc);
            await SendLegacyTeleportFrameAsync(context, context.Connection, spawn.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyNpcSpawn " +
                    $"{FormatSession(context)} map={character.MapId} npcCount={sentCount}.")
                .ConfigureAwait(false);
        }
        else
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacyNpcSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)} " +
                            $"loadedNpcCount={npcs.Count}; source LegacyInstance.Info dynamic map uses custom instance-scoped spawns.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer found no visible LegacyNpcSpawn " +
                            $"{FormatSession(context)} {FormatStaticNpcVisibilityDiagnostic(character, "no-visible-row-within-range")} " +
                            $"loadedNpcCount={npcs.Count}" +
                            $"{FormatNearestStaticNpcDiagnostic(character, npcs)}.")
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static async ValueTask SendVisibleFurnitureNpcsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        var sendOnlyNewlyVisible = previousCharacter is not null;
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyFurnitureNpcProjection visibility " +
                        $"{FormatSession(context)}; map id is out of Int32 range.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (context.WorldData is not { } worldData)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyFurnitureNpcProjection visibility " +
                        $"{FormatSession(context)}; world data is not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!worldData.FurnitureByMapId.TryGetValue((int)character.MapId, out var furnitureRows))
        {
            return;
        }

        var sentCount = 0;
        var skippedProjectionCount = 0;
        foreach (var furniture in furnitureRows)
        {
            if (!CanSeeFurnitureNpc(character, furniture))
            {
                continue;
            }

            if (previousCharacter is { } previous &&
                CanSeeFurnitureNpc(previous, furniture))
            {
                continue;
            }

            if (!LegacyFurnitureNpcProjection.TryCreate(furniture, out var projection))
            {
                skippedProjectionCount++;
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection, projection.ToNpcSpawnPacket().ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyFurnitureNpcProjection " +
                    $"{FormatSession(context)} map={character.MapId} furnitureCount={sentCount}.")
                .ConfigureAwait(false);
        }
        else if (skippedProjectionCount > 0 && !sendOnlyNewlyVisible)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyFurnitureNpcProjection visibility " +
                    $"{FormatSession(context)} map={character.MapId} invalidProjectionCount={skippedProjectionCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendVisibleChristmasNpcsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        var sendOnlyNewlyVisible = previousCharacter is not null;
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyChristmasNpcProjection visibility " +
                        $"{FormatSession(context)}; map id is out of Int32 range.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (context.WorldData is not { } worldData)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyChristmasNpcProjection visibility " +
                        $"{FormatSession(context)}; world data is not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!worldData.ChristmasNpcsByMapId.TryGetValue((int)character.MapId, out var christmasNpcs))
        {
            return;
        }

        var sentCount = 0;
        var skippedProjectionCount = 0;
        foreach (var christmasNpc in christmasNpcs)
        {
            if (!CanSeeChristmasNpc(character, christmasNpc))
            {
                continue;
            }

            if (previousCharacter is { } previous &&
                CanSeeChristmasNpc(previous, christmasNpc))
            {
                continue;
            }

            if (!LegacyChristmasNpcProjection.TryCreate(christmasNpc, out var projection))
            {
                skippedProjectionCount++;
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection, projection.ToNpcSpawnPacket().ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyChristmasNpcProjection " +
                    $"{FormatSession(context)} map={character.MapId} christmasNpcCount={sentCount}.")
                .ConfigureAwait(false);
        }
        else if (skippedProjectionCount > 0 && !sendOnlyNewlyVisible)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyChristmasNpcProjection visibility " +
                    $"{FormatSession(context)} map={character.MapId} invalidProjectionCount={skippedProjectionCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendVisibleSobNpcsAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken,
        CharacterSnapshot? previousCharacter = null)
    {
        var sendOnlyNewlyVisible = previousCharacter is not null;
        if (character.MapId is < int.MinValue or > int.MaxValue)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacySobNpcSpawn visibility " +
                        $"{FormatSession(context)}; map id is out of Int32 range.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (context.WorldData is not { } worldData)
        {
            if (!sendOnlyNewlyVisible)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP skipped LegacySobNpcSpawn visibility " +
                        $"{FormatSession(context)}; world data is not loaded.")
                    .ConfigureAwait(false);
            }

            return;
        }

        if (!worldData.SobNpcsByMapId.TryGetValue((int)character.MapId, out var sobNpcs))
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacySobNpcSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)}; " +
                            "source LegacyInstance.Info dynamic map uses custom instance-scoped spawns.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "PARITY-GAP no LegacySobNpcSpawn rows loaded " +
                            $"{FormatSession(context)} {FormatSobNpcVisibilityDiagnostic(character, "no-rows-loaded")}.")
                        .ConfigureAwait(false);
                }
            }

            return;
        }

        var sentCount = 0;
        foreach (var sobNpc in sobNpcs)
        {
            if (!CanSeeSobNpc(character, sobNpc))
            {
                continue;
            }

            if (previousCharacter is { } previous &&
                CanSeeSobNpc(previous, sobNpc))
            {
                continue;
            }

            var visibleSobNpc = context.SobNpcStates?.GetVisible(sobNpc) ?? sobNpc;
            var spawn = LegacySobNpcSpawnPacket.FromSobNpc(visibleSobNpc);
            await SendLegacyTeleportFrameAsync(context, context.Connection, spawn.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacySobNpcSpawn " +
                    $"{FormatSession(context)} map={character.MapId} sobNpcCount={sentCount}.")
                .ConfigureAwait(false);
        }
        else
        {
            if (!sendOnlyNewlyVisible)
            {
                if (IsLegacyInstanceInfoDynamicMap(character))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped normal LegacySobNpcSpawn visibility " +
                            $"{FormatSession(context)} map={character.MapId} dynamicId={ReadCharacterDynamicId(character)} " +
                            $"loadedSobNpcCount={sobNpcs.Count}; source LegacyInstance.Info dynamic map uses custom instance-scoped spawns.")
                        .ConfigureAwait(false);
                }
                else
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer found no visible LegacySobNpcSpawn " +
                            $"{FormatSession(context)} {FormatSobNpcVisibilityDiagnostic(character, "no-visible-row-within-range")} " +
                            $"loadedSobNpcCount={sobNpcs.Count}" +
                            $"{FormatNearestSobNpcDiagnostic(character, sobNpcs)}.")
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private static async ValueTask SendVisibleRouletteTablesAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        if (character.MapId != LegacyRouletteTablePacket.MapId ||
            context.WorldData is not { } worldData)
        {
            return;
        }

        var sentCount = 0;
        foreach (var table in worldData.RouletteTables)
        {
            var packet = LegacyRouletteTablePacket.FromRouletteTable(table);
            if (LegacyGetDistance(character.X, character.Y, packet.X, packet.Y) > LegacyRouletteTablePacket.ViewThreshold)
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection, packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyRouletteTable " +
                    $"{FormatSession(context)} map={character.MapId} tableCount={sentCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendVisiblePokerTablesAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        CancellationToken cancellationToken)
    {
        if (character.MapId is not LegacyPokerTablePacket.ShowHandMapId and not LegacyPokerTablePacket.CpsMapId ||
            context.WorldData is not { } worldData)
        {
            return;
        }

        var sentCount = 0;
        foreach (var table in worldData.PokerTables)
        {
            var packet = LegacyPokerTablePacket.FromPokerTable(table);
            if (packet.MapId != character.MapId ||
                LegacyGetDistance(character.X, character.Y, packet.X, packet.Y) > LegacyViewThreshold)
            {
                continue;
            }

            await SendLegacyTeleportFrameAsync(context, context.Connection, packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        if (sentCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent LegacyPokerTable " +
                    $"{FormatSession(context)} map={character.MapId} tableCount={sentCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool CanSeeStaticNpc(CharacterSnapshot character, Npc npc)
    {
        return character.MapId == npc.MapId &&
            CanSeeStaticNpcDynamicId(character, npc) &&
            LegacyGetDistance(character.X, character.Y, npc.X, npc.Y) <= LegacyStaticNpcViewThreshold;
    }

    private static bool CanSeeFurnitureNpc(CharacterSnapshot character, Furniture furniture)
    {
        return character.MapId == furniture.MapId &&
            LegacyGetDistance(character.X, character.Y, furniture.X, furniture.Y) <= LegacyViewThreshold;
    }

    private static bool CanSeeChristmasNpc(CharacterSnapshot character, ChristmasNpc npc)
    {
        return character.MapId == npc.MapId &&
            ReadCharacterDynamicId(character) == 0 &&
            LegacyGetDistance(character.X, character.Y, npc.X, npc.Y) <= LegacyStaticNpcViewThreshold;
    }

    private static bool CanSeeSellNpc(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint npcId)
    {
        return npcId <= int.MaxValue &&
            context.WorldData?.NpcsById.TryGetValue((int)npcId, out var npcs) == true &&
            npcs.Any(npc => CanSeeStaticNpc(character, npc));
    }

    private static bool CanSeeBuyShop(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint shopId)
    {
        return shopId == LegacyRemoteShopId || CanSeeSellNpc(context, character, shopId);
    }

    private static bool CanSeeSobNpc(CharacterSnapshot character, SobNpc sobNpc)
    {
        return character.MapId == sobNpc.MapId &&
            ReadCharacterDynamicId(character) == 0 &&
            LegacyGetDistance(character.X, character.Y, sobNpc.X, sobNpc.Y) <= LegacyViewThreshold;
    }

    private static bool CanSeeStaticNpcDynamicId(CharacterSnapshot character, Npc npc)
    {
        var dynamicId = ReadCharacterDynamicId(character);
        return dynamicId == 0 || IsLegacyStaticNpcAllowDynamic(npc);
    }

    private static bool IsLegacyStaticNpcAllowDynamic(Npc npc)
    {
        return npc.Id is 16851 or 168052 ||
            npc.OptionalFlag == 1;
    }

    private static string FormatStaticNpcVisibilityDiagnostic(CharacterSnapshot character, string reason)
    {
        return $"map={character.MapId} x={character.X} y={character.Y} range={LegacyStaticNpcViewThreshold} " +
            $"source=MrRayzo.Role.RoleView.Role(MapObjectType.Npc) reason={reason}";
    }

    private static string FormatSobNpcVisibilityDiagnostic(CharacterSnapshot character, string reason)
    {
        return $"map={character.MapId} x={character.X} y={character.Y} range={LegacyViewThreshold} " +
            $"source=MrRayzo.Role.RoleView.Role(MapObjectType.SobNpc) reason={reason}";
    }

    private static string FormatNearestStaticNpcDiagnostic(CharacterSnapshot character, IReadOnlyList<Npc> npcs)
    {
        if (npcs.Count == 0)
        {
            return " nearestNpc=<none>";
        }

        var nearest = npcs[0];
        var nearestDistance = LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y);
        foreach (var npc in npcs.Skip(1))
        {
            var distance = LegacyGetDistance(character.X, character.Y, npc.X, npc.Y);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = npc;
            nearestDistance = distance;
        }

        return $" nearestNpcId={nearest.Id} nearestNpcX={nearest.X} nearestNpcY={nearest.Y} " +
            $"nearestNpcDistance={nearestDistance} visibilityThreshold={LegacyStaticNpcViewThreshold}" +
            FormatNpcSourceRowDiagnostic(nearest);
    }

    private static string FormatNearestSobNpcDiagnostic(CharacterSnapshot character, IReadOnlyList<SobNpc> sobNpcs)
    {
        if (sobNpcs.Count == 0)
        {
            return " nearestSobNpc=<none>";
        }

        var nearest = sobNpcs[0];
        var nearestDistance = LegacyGetDistance(character.X, character.Y, nearest.X, nearest.Y);
        foreach (var sobNpc in sobNpcs.Skip(1))
        {
            var distance = LegacyGetDistance(character.X, character.Y, sobNpc.X, sobNpc.Y);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = sobNpc;
            nearestDistance = distance;
        }

        return $" nearestSobNpcId={nearest.Id} nearestSobNpcName={nearest.Name} nearestSobNpcX={nearest.X} " +
            $"nearestSobNpcY={nearest.Y} nearestSobNpcDistance={nearestDistance} visibilityThreshold={LegacyViewThreshold}" +
            FormatStaticRoleSourceRowDiagnostic(nearest.SourceLine, nearest.RawLine);
    }

    private static string FormatNpcSourceRowDiagnostic(Npc npc)
    {
        var rawLine = string.IsNullOrWhiteSpace(npc.RawLine)
            ? BuildNpcRawRow(npc)
            : npc.RawLine;
        return FormatStaticRoleSourceRowDiagnostic(npc.SourceLine, rawLine);
    }

    private static string BuildNpcRawRow(Npc npc)
    {
        var values = new List<string>
        {
            npc.Id.ToString(CultureInfo.InvariantCulture),
            npc.Kind.ToString(CultureInfo.InvariantCulture),
            npc.Lookface.ToString(CultureInfo.InvariantCulture),
            npc.MapId.ToString(CultureInfo.InvariantCulture),
            npc.X.ToString(CultureInfo.InvariantCulture),
            npc.Y.ToString(CultureInfo.InvariantCulture),
        };
        if (npc.OptionalFlag is { } optionalFlag)
        {
            values.Add(optionalFlag.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(',', values);
    }

    private static string FormatStaticRoleSourceRowDiagnostic(int sourceLine, string rawLine)
    {
        var builder = new StringBuilder();
        if (sourceLine > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $" sourceLine={sourceLine}");
        }

        if (!string.IsNullOrWhiteSpace(rawLine))
        {
            builder.Append(" rawRow=\"");
            builder.Append(SanitizeStaticRoleDiagnosticValue(rawLine));
            builder.Append('"');
        }

        return builder.ToString();
    }

    private static string SanitizeStaticRoleDiagnosticValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static bool TryFindVisibleStaticNpcByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out Npc npc)
    {
        npc = default!;
        if (uid > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.NpcsById.TryGetValue((int)uid, out var npcs))
        {
            return false;
        }

        foreach (var candidate in npcs)
        {
            if (CanSeeStaticNpc(character, candidate))
            {
                npc = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindVisibleFurnitureNpcByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyFurnitureNpcProjection projection)
    {
        projection = default!;
        if (uid > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.FurnitureById.TryGetValue((int)uid, out var furniture) ||
            !CanSeeFurnitureNpc(character, furniture) ||
            !LegacyFurnitureNpcProjection.TryCreate(furniture, out projection))
        {
            return false;
        }

        return projection.MatchesRequestEntity(uid);
    }

    private static bool TryFindVisibleChristmasNpcByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out LegacyChristmasNpcProjection projection)
    {
        projection = default!;
        if (uid > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.ChristmasNpcsById.TryGetValue((int)uid, out var christmasNpcs))
        {
            return false;
        }

        foreach (var candidate in christmasNpcs)
        {
            if (!CanSeeChristmasNpc(character, candidate) ||
                !LegacyChristmasNpcProjection.TryCreate(candidate, out projection) ||
                !projection.MatchesRequestEntity(uid))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryFindVisibleSobNpcByUid(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint uid,
        out SobNpc sobNpc)
    {
        sobNpc = default!;
        if (uid > int.MaxValue ||
            context.WorldData is not { } worldData ||
            !worldData.SobNpcsById.TryGetValue((int)uid, out var sobNpcs))
        {
            return false;
        }

        foreach (var candidate in sobNpcs)
        {
            if (CanSeeSobNpc(character, candidate))
            {
                sobNpc = candidate;
                return true;
            }
        }

        return false;
    }

}
