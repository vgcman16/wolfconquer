using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyMoveLineGuildWarEndpointCheck CheckLegacyMoveLineGuildWarEndpoint(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort endpointX,
        ushort endpointY)
    {
        if (session.Character.MapId != LegacyGuildWarJumpRules.GuildWarMapId ||
            context.Terrain is not { } terrain ||
            !terrain.TryGetTerrain(LegacyGuildWarJumpRules.GuildWarMapId, out _))
        {
            return new LegacyMoveLineGuildWarEndpointCheck(
                Applies: false,
                Allowed: true,
                CurrentTerrainMask: 0,
                NextTerrainMask: 0,
                FloorType: 0,
                LeftGateOpen: false,
                RightGateOpen: false,
                X: endpointX,
                Y: endpointY);
        }

        var location = terrain.QueryLocation(LegacyGuildWarJumpRules.GuildWarMapId, endpointX, endpointY);
        if (!location.IsInBounds || location.Surface is not { } floorType)
        {
            return new LegacyMoveLineGuildWarEndpointCheck(
                Applies: false,
                Allowed: true,
                CurrentTerrainMask: 0,
                NextTerrainMask: 0,
                FloorType: 0,
                LeftGateOpen: false,
                RightGateOpen: false,
                X: endpointX,
                Y: endpointY);
        }

        var currentTerrainMask = ResolveLegacyGuildWarTerrainMask(context, session);
        var leftGateOpen = ReadAttributeBool(
            session.Character.Attributes,
            RuntimeLegacyGuildWarLeftGateOpenAttribute,
            "LeftGateOpen",
            "GuildWarLeftGateOpen");
        var rightGateOpen = ReadAttributeBool(
            session.Character.Attributes,
            RuntimeLegacyGuildWarRightGateOpenAttribute,
            "RightGateOpen",
            "GuildWarRightGateOpen");
        var allowed = LegacyGuildWarJumpRules.TryGetNextTerrainMask(
            currentTerrainMask,
            endpointX,
            endpointY,
            floorType,
            leftGateOpen,
            rightGateOpen,
            out var nextTerrainMask);

        return new LegacyMoveLineGuildWarEndpointCheck(
            Applies: true,
            Allowed: allowed,
            CurrentTerrainMask: currentTerrainMask,
            NextTerrainMask: nextTerrainMask,
            FloorType: floorType,
            LeftGateOpen: leftGateOpen,
            RightGateOpen: rightGateOpen,
            X: endpointX,
            Y: endpointY);
    }

    private static int ResolveLegacyGuildWarTerrainMask(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        if (TryReadLegacyGuildWarTerrainMaskAttribute(session.Character.Attributes, out var terrainMask))
        {
            return terrainMask;
        }

        if (session.Character.MapId is < int.MinValue or > int.MaxValue ||
            session.Character.X is < int.MinValue or > int.MaxValue ||
            session.Character.Y is < int.MinValue or > int.MaxValue ||
            context.Terrain is not { } terrain)
        {
            return 0;
        }

        var currentLocation = terrain.QueryLocation(
            (int)session.Character.MapId,
            (int)session.Character.X,
            (int)session.Character.Y);
        return currentLocation.Surface ?? 0;
    }

    private static bool TryReadLegacyGuildWarTerrainMaskAttribute(
        IReadOnlyDictionary<string, string> attributes,
        out int terrainMask)
    {
        foreach (var key in new[]
                 {
                     RuntimeLegacyGuildWarTerrainMaskAttribute,
                     RuntimeLegacyGuildWarTerrainMaskAliasAttribute,
                     RuntimeLegacyGuildWarCurrentTerrainMaskAttribute,
                     "GuildWarTerrainMask",
                 })
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out terrainMask))
            {
                return true;
            }

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                terrainMask = parsed < int.MinValue
                    ? int.MinValue
                    : parsed > int.MaxValue
                        ? int.MaxValue
                        : (int)parsed;
                return true;
            }
        }

        terrainMask = 0;
        return false;
    }

    private static WorldSession UpdateLegacyGuildWarTerrainMask(
        WorldPacketHandlerContext context,
        WorldSession session,
        int terrainMask)
    {
        var rawTerrainMask = terrainMask.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyGuildWarTerrainMaskAttribute] = rawTerrainMask,
            [RuntimeLegacyGuildWarTerrainMaskAliasAttribute] = rawTerrainMask,
            [RuntimeLegacyGuildWarCurrentTerrainMaskAttribute] = rawTerrainMask,
        };

        var updatedSession = session with
        {
            Character = session.Character with
            {
                Attributes = attributes,
            },
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        return updatedSession;
    }

    private static async ValueTask SendLegacyMoveLineGuildWarPullbackAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMoveLineGuildWarEndpointCheck endpointCheck,
        CancellationToken cancellationToken)
    {
        await SendLegacyGuildWarPullbackAsync(
                context,
                context.Connection,
                session,
                endpointCheck,
                sourceName: "MoveLine",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyGuildWarPullbackAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        LegacyMoveLineGuildWarEndpointCheck endpointCheck,
        string sourceName,
        CancellationToken cancellationToken)
    {
        await connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(LegacyGuildWarIllegalJumpMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySetLocationAsync(context, connection, session.Character, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer blocked legacy {sourceName} GuildWar gate crossing " +
                $"{FormatSession(context)} fromX={session.Character.X} fromY={session.Character.Y} " +
                $"endpointX={endpointCheck.X} endpointY={endpointCheck.Y} " +
                $"terrainMask={endpointCheck.CurrentTerrainMask} floorType={endpointCheck.FloorType} " +
                $"leftGateOpen={endpointCheck.LeftGateOpen} rightGateOpen={endpointCheck.RightGateOpen}.")
            .ConfigureAwait(false);
    }
}
