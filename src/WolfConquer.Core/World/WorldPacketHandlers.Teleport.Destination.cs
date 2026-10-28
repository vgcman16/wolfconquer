using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static LegacyTeleportDestinationReduction ReduceLegacyTeleportDestination(
        WorldPacketHandlerContext context,
        CharacterSnapshot source,
        LegacyTeleportDestination requestedDestination)
    {
        var requestedMapExists = LegacyTeleportMapExists(context, requestedDestination.MapId);
        var firstDestinationLocationIsValid = requestedDestination.MapId != 6_000 ||
            !requestedMapExists ||
            LegacyTeleportLocationIsValid(
                context,
                requestedDestination.MapId,
                requestedDestination.X,
                requestedDestination.Y);
        return LegacyTeleportDestinationReducer.Reduce(
            source,
            requestedDestination,
            requestedMapExists,
            firstDestinationLocationIsValid);
    }

    internal static async ValueTask<WorldSession?> ExecuteLegacyTeleportDestinationAsync(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        LegacyTeleportDestination requestedDestination,
        uint timestamp,
        CancellationToken cancellationToken,
        WorldSession? expectedSession = null)
    {
        expectedSession ??= sourceSession;
        var reduction = ReduceLegacyTeleportDestination(
            context,
            sourceSession.Character,
            requestedDestination);
        var firstSession = sourceSession with
        {
            Character = reduction.FirstTransition.Character,
        };

        var committedFirst = await TryPublishLegacyTeleportDestinationAsync(
                context,
                expectedSession,
                firstSession,
                "first",
                cancellationToken)
            .ConfigureAwait(false);
        if (committedFirst is null)
        {
            return null;
        }
        firstSession = committedFirst;

        await SendLegacyTeleportLeaveToVisiblePeersAsync(context, sourceSession, cancellationToken)
            .ConfigureAwait(false);
        var appliedSession = await SendLegacyTeleportTransitionAsync(
                context with { Session = firstSession },
                firstSession,
                timestamp,
                cancellationToken,
                sourceMapId: sourceSession.Character.MapId)
            .ConfigureAwait(false);
        if (appliedSession is null || reduction.Map6000Correction is null)
        {
            return appliedSession;
        }

        var correction = LegacyTeleportDestinationReducer.Reduce(
            appliedSession.Character,
            new LegacyTeleportDestination(6_000, 32, 73),
            requestedMapExists: true,
            firstDestinationLocationIsValid: true);
        var correctionSession = appliedSession with
        {
            Character = correction.FirstTransition.Character,
        };
        var correctionContext = context with { Session = appliedSession };
        var committedCorrection = await TryPublishLegacyTeleportDestinationAsync(
                correctionContext,
                appliedSession,
                correctionSession,
                "map-6000 correction",
                cancellationToken)
            .ConfigureAwait(false);
        if (committedCorrection is null)
        {
            return null;
        }
        correctionSession = committedCorrection;

        await SendLegacyTeleportLeaveToVisiblePeersAsync(
                correctionContext,
                appliedSession,
                cancellationToken)
            .ConfigureAwait(false);
        return await SendLegacyTeleportTransitionAsync(
                context with { Session = correctionSession },
                correctionSession,
                timestamp,
                cancellationToken,
                sourceMapId: appliedSession.Character.MapId)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> TryPublishLegacyTeleportDestinationAsync(
        WorldPacketHandlerContext context,
        WorldSession expectedSession,
        WorldSession destinationSession,
        string phase,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy GameClient.Teleport destination publication " +
                    $"phase={phase} expectedUid={expectedSession.Character.Uid} " +
                    $"sourceMap={expectedSession.Character.MapId} destinationMap={destinationSession.Character.MapId}; " +
                    "WorldSessionStore is unavailable.")
                .ConfigureAwait(false);
            return null;
        }

        if (TryCommitOwnedLegacyTeleport(context, expectedSession, destinationSession, out var final))
        {
            return final;
        }

        sessions.TryGet(context.Connection, out var currentSession);
        var currentUid = currentSession?.Character.Uid.ToString(CultureInfo.InvariantCulture) ?? "missing";
        var currentMap = currentSession?.Character.MapId.ToString(CultureInfo.InvariantCulture) ?? "missing";
        await context.Log.WriteLineAsync(
                "WorldServer skipped stale legacy GameClient.Teleport destination publication " +
                $"phase={phase} expectedUid={expectedSession.Character.Uid} " +
                $"sourceMap={expectedSession.Character.MapId} destinationMap={destinationSession.Character.MapId} " +
                $"currentUid={currentUid} currentMap={currentMap}; " +
                "WorldSessionStore.TryCompareAndSet rejected the source session before leave or transition packets were sent.")
            .ConfigureAwait(false);
        return null;
    }

    private static bool LegacyTeleportMapExists(
        WorldPacketHandlerContext context,
        long mapId)
    {
        if (mapId is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        var checkedMapId = checked((int)mapId);
        if (context.TeleportTerrain is { } teleportTerrain)
        {
            return teleportTerrain.TryGetTerrain(checkedMapId, out _);
        }

        // Isolated handler tests and embedders without a terrain authority exercise packet behavior,
        // not GameMap.dat loading. The production handler always supplies TeleportTerrain.
        return true;
    }

    private static bool LegacyTeleportLocationIsValid(
        WorldPacketHandlerContext context,
        long mapId,
        long x,
        long y)
    {
        if (mapId is < int.MinValue or > int.MaxValue ||
            x is < int.MinValue or > int.MaxValue ||
            y is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        var checkedMapId = checked((int)mapId);
        var checkedX = checked((int)x);
        var checkedY = checked((int)y);
        if (context.TeleportTerrain is { } teleportTerrain &&
            teleportTerrain.IsWalkable(checkedMapId, checkedX, checkedY))
        {
            return true;
        }

        if (context.WorldData is not { } worldData)
        {
            return false;
        }

        return (worldData.NpcsByMapId.TryGetValue(checkedMapId, out var npcs) &&
                npcs.Any(npc => npc.X == checkedX && npc.Y == checkedY)) ||
            (worldData.SobNpcsByMapId.TryGetValue(checkedMapId, out var sobNpcs) &&
                sobNpcs.Any(npc => npc.X == checkedX && npc.Y == checkedY));
    }

    private static WorldSession CreateLegacyTeleportMutationSourceSession(
        WorldSession sourceSession,
        WorldSession mutationSession)
    {
        var sourceCharacter = sourceSession.Character with
        {
            Attributes = CopyCharacterAttributes(mutationSession.Character.Attributes),
        };
        sourceCharacter = WithCharacterAttribute(
            sourceCharacter,
            "DynamicID",
            ReadCharacterDynamicId(sourceSession.Character));
        return mutationSession with { Character = sourceCharacter };
    }

    private static Dictionary<string, string> CreateLegacyCommittedTeleportPersistenceValues(
        CharacterSnapshot teleportedCharacter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Map"] = teleportedCharacter.MapId.ToString(CultureInfo.InvariantCulture),
            ["X"] = teleportedCharacter.X.ToString(CultureInfo.InvariantCulture),
            ["Y"] = teleportedCharacter.Y.ToString(CultureInfo.InvariantCulture),
            ["PMap"] = teleportedCharacter.PreviousMapId.ToString(CultureInfo.InvariantCulture),
            ["PMapX"] = teleportedCharacter.PreviousMapX.ToString(CultureInfo.InvariantCulture),
            ["PMapY"] = teleportedCharacter.PreviousMapY.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = ReadCharacterDynamicId(teleportedCharacter).ToString(CultureInfo.InvariantCulture),
        };
        var previousDynamicId = ReadAttributeString(
            teleportedCharacter.Attributes,
            "PDinamycID");
        if (!string.IsNullOrWhiteSpace(previousDynamicId))
        {
            values["PDinamycID"] = previousDynamicId;
        }

        return values;
    }
}
