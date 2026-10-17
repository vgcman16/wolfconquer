namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static async ValueTask<LegacyTeleportPreflightOutcome> PrepareLegacyTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        LegacyTeleportRequest request,
        CancellationToken cancellationToken = default)
    {
        var entrySideEffects = await ApplyLegacyTeleportEntrySideEffectsAsync(
                context,
                sourceSession,
                request,
                cancellationToken)
            .ConfigureAwait(false);
        sourceSession = entrySideEffects.Session;

        var character = sourceSession.Character;
        var inQualifier = IsLegacyTeleportInQualifier(context, character);
        var isWatching = IsLegacyTeleportWatching(context, character);
        if (entrySideEffects.Denial is { } entryDenial)
        {
            await LogLegacyTeleportDenialAsync(
                    context,
                    sourceSession,
                    request,
                    entryDenial,
                    inQualifier,
                    isWatching)
                .ConfigureAwait(false);
            return new LegacyTeleportPreflightOutcome(
                request,
                sourceSession,
                entryDenial,
                entrySideEffects.QualifierEndRequested,
                entrySideEffects.EndedQualifier,
                entrySideEffects.RemovedRide,
                inQualifier,
                isWatching);
        }

        var sourceMapId = character.MapId;
        var isProjectManager = ReadAttributeBool(
            character.Attributes,
            "ProjectManager",
            "IsProjectManager");
        var isDead = !IsLegacyTeleportCharacterAlive(character);
        var setLocationType = ReadAttributeUInt64(character.Attributes, "SetLocationType");
        var onMyOwnServer = LegacyRealmTransferServicesIsOnOwnServer(sourceSession);

        LegacyTeleportDenial? denial = null;
        if (sourceMapId == 6_001 && !isProjectManager && !request.CanTeleport)
        {
            denial = new LegacyTeleportDenial(
                LegacyTeleportDenialKind.SourceMapGuard,
                "source map 6001 denies non-ProjectManager teleport unless CanTeleport is true",
                "GameClient.Teleport lines 1737-1740");
        }
        else if (sourceMapId == 1_038 && isDead && !request.CanTeleport)
        {
            denial = new LegacyTeleportDenial(
                LegacyTeleportDenialKind.SourceMapGuard,
                "dead character on source map 1038 requires CanTeleport",
                "GameClient.Teleport lines 1737-1740");
        }
        else if (setLocationType is not 1 and not 2 and not 22 &&
                 !onMyOwnServer &&
                 request.RequestedMapId is not 1_002 and not 3_935 and not 10_137 and not 8_709 and not 5_339 &&
                 !inQualifier &&
                 !isWatching)
        {
            denial = new LegacyTeleportDenial(
                LegacyTeleportDenialKind.CrossServerGuard,
                "cross-server character requires SetLocationType 1, 2, or 22, own-server ownership, " +
                "destination map 1002, 3935, 10137, 8709, or 5339, qualifier membership, or watching state",
                "GameClient.Teleport lines 1744-1747; GameClient.InQualifier/IsWatching lines 743-751; " +
                "Player.OnMyOwnServer line 1363");
        }

        if (denial is { } guardDenial)
        {
            await LogLegacyTeleportDenialAsync(
                    context,
                    sourceSession,
                    request,
                    guardDenial,
                    inQualifier,
                    isWatching)
                .ConfigureAwait(false);
        }

        return new LegacyTeleportPreflightOutcome(
            request,
            sourceSession,
            denial,
            entrySideEffects.QualifierEndRequested,
            entrySideEffects.EndedQualifier,
            entrySideEffects.RemovedRide,
            inQualifier,
            isWatching);
    }

    private static async ValueTask LogLegacyTeleportDenialAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyTeleportRequest request,
        LegacyTeleportDenial denial,
        bool inQualifier,
        bool isWatching)
    {
        var character = session.Character;
        var isProjectManager = ReadAttributeBool(
            character.Attributes,
            "ProjectManager",
            "IsProjectManager");
        var isDead = !IsLegacyTeleportCharacterAlive(character);
        var setLocationType = ReadAttributeUInt64(character.Attributes, "SetLocationType");
        var onMyOwnServer = LegacyRealmTransferServicesIsOnOwnServer(session);
        await context.Log.WriteLineAsync(
                "WorldServer denied legacy GameClient.Teleport preflight " +
                $"{FormatSession(context with { Session = session })} source={request.SourceMethod} " +
                $"sourceMap={character.MapId} requestedMap={request.RequestedMapId} " +
                $"requestedDynamic={request.RequestedDynamicId} canTeleport={request.CanTeleport} " +
                $"projectManager={isProjectManager} dead={isDead} setLocationType={setLocationType} " +
                $"onMyOwnServer={onMyOwnServer} inQualifier={inQualifier} watching={isWatching} " +
                $"denial={denial.Kind} reason=\"{denial.Reason}\"; source={denial.SourceEvidence}.")
            .ConfigureAwait(false);
    }
}
