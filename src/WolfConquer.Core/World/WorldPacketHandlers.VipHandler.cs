using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyVipTeleportDestination(long MapId, long X, long Y);

    private const string RuntimeVipTeleportSecondsAttribute = "RuntimeVipTeleportSeconds";
    private const string RuntimeVipTeamTeleportSecondsAttribute = "RuntimeVipTeamTeleportSeconds";

    [PacketHandler(WorldPacketOpcodes.LegacyVipHandler)]
    private static async ValueTask HandleLegacyVipHandlerAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyVipHandlerPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyVipHandler packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyVipHandler " +
                $"{FormatSession(context)} type={packet.TeleportType} location={packet.Location} " +
                $"countdown={packet.Countdown} name={packet.Name}.")
            .ConfigureAwait(false);

        if (context.Session is not { } session)
        {
            return;
        }

        if (packet.TeleportType == LegacyVipTeleportType.TeammateConfirmation)
        {
            return;
        }

        if (!TryGetLegacyVipTeleportDestination(packet.Location, out var destination))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyVipHandler " +
                    $"{FormatSession(context)}; unknown VIP teleport location={packet.Location}.")
                .ConfigureAwait(false);
            return;
        }

        var sourceSession = session;
        if (!TryApplyLegacyVipTeleportCooldown(
                packet.TeleportType,
                sourceSession,
                out session,
                out var cooldownMessage))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(cooldownMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer blocked LegacyVipHandler cooldown " +
                    $"{FormatSession(context)} type={packet.TeleportType} location={packet.Location}.")
                .ConfigureAwait(false);
            return;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                sourceSession,
                new LegacyTeleportRequest(
                    "MsgVipHandler.Handler -> MsgVipHandler.Teleport -> GameClient.Teleport",
                    destination.MapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        session = CarryLegacyTeleportEntrySideEffects(preflight, session);

        if (packet.TeleportType == LegacyVipTeleportType.TeamTeleport)
        {
            await SendLegacyVipTeamTeleportConfirmationsAsync(
                    context,
                    session,
                    packet.Location,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(
                    destination.MapId,
                    destination.X,
                    destination.Y),
                timestamp: 0,
                cancellationToken,
                expectedSession: preflight.Session)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return;
        }

        var updatedContext = context with { Session = updatedSession };
        var teleported = updatedSession.Character;

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyVipHandler teleport " +
                $"{FormatSession(updatedContext)} map={teleported.MapId} x={teleported.X} y={teleported.Y}.")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyVipTeleportDestination(
        LegacyVipTeleportLocation location,
        out LegacyVipTeleportDestination destination)
    {
        destination = location switch
        {
            LegacyVipTeleportLocation.TwinCity or
            LegacyVipTeleportLocation.TCSquare => new LegacyVipTeleportDestination(1_002, 410, 354),
            LegacyVipTeleportLocation.WPAltar => new LegacyVipTeleportDestination(1_002, 439, 666),
            LegacyVipTeleportLocation.WPApparition => new LegacyVipTeleportDestination(1_002, 250, 656),
            LegacyVipTeleportLocation.WPPoltergeist => new LegacyVipTeleportDestination(1_002, 104, 581),
            LegacyVipTeleportLocation.WPTurtledove => new LegacyVipTeleportDestination(1_002, 416, 608),
            LegacyVipTeleportLocation.PhoenixCastle or
            LegacyVipTeleportLocation.PCSquare => new LegacyVipTeleportDestination(1_011, 184, 275),
            LegacyVipTeleportLocation.MFWaterCave => new LegacyVipTeleportDestination(1_011, 380, 31),
            LegacyVipTeleportLocation.MFVillage => new LegacyVipTeleportDestination(1_011, 785, 472),
            LegacyVipTeleportLocation.MFLake => new LegacyVipTeleportDestination(1_011, 369, 568),
            LegacyVipTeleportLocation.MFMineCave => new LegacyVipTeleportDestination(1_011, 924, 560),
            LegacyVipTeleportLocation.MFBridge => new LegacyVipTeleportDestination(1_011, 648, 567),
            LegacyVipTeleportLocation.MFToApeCity => new LegacyVipTeleportDestination(1_011, 475, 841),
            LegacyVipTeleportLocation.ApeCity or
            LegacyVipTeleportLocation.ACSquare => new LegacyVipTeleportDestination(1_020, 567, 567),
            LegacyVipTeleportLocation.ACSouth => new LegacyVipTeleportDestination(1_020, 699, 640),
            LegacyVipTeleportLocation.ACEast => new LegacyVipTeleportDestination(1_020, 624, 337),
            LegacyVipTeleportLocation.ACNorth => new LegacyVipTeleportDestination(1_020, 200, 224),
            LegacyVipTeleportLocation.ACWest => new LegacyVipTeleportDestination(1_020, 322, 621),
            LegacyVipTeleportLocation.DesertCity or
            LegacyVipTeleportLocation.DCSquare => new LegacyVipTeleportDestination(1_000, 502, 655),
            LegacyVipTeleportLocation.DCSouth => new LegacyVipTeleportDestination(1_000, 758, 750),
            LegacyVipTeleportLocation.DCMoonSpring => new LegacyVipTeleportDestination(1_000, 291, 450),
            LegacyVipTeleportLocation.DCAncientMaze => new LegacyVipTeleportDestination(1_000, 87, 321),
            LegacyVipTeleportLocation.BirdIsland or
            LegacyVipTeleportLocation.BISquare => new LegacyVipTeleportDestination(1_015, 725, 574),
            LegacyVipTeleportLocation.IslandVillage => new LegacyVipTeleportDestination(1_015, 329, 375),
            LegacyVipTeleportLocation.BIWest => new LegacyVipTeleportDestination(1_015, 520, 730),
            LegacyVipTeleportLocation.BINorth => new LegacyVipTeleportDestination(1_015, 400, 303),
            LegacyVipTeleportLocation.BIEast => new LegacyVipTeleportDestination(1_015, 572, 370),
            _ => default,
        };

        return destination != default;
    }

    private static bool TryApplyLegacyVipTeleportCooldown(
        LegacyVipTeleportType teleportType,
        WorldSession session,
        out WorldSession updatedSession,
        out string cooldownMessage)
    {
        updatedSession = session;
        cooldownMessage = string.Empty;

        var key = teleportType switch
        {
            LegacyVipTeleportType.SelfTeleport => RuntimeVipTeleportSecondsAttribute,
            LegacyVipTeleportType.TeamTeleport => RuntimeVipTeamTeleportSecondsAttribute,
            _ => string.Empty,
        };

        if (key.Length == 0)
        {
            return true;
        }

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lastSeconds = checked((long)ReadAttributeUInt64(session.Character.Attributes, key));
        if (lastSeconds > 0 && nowSeconds <= lastSeconds + 60)
        {
            var waitUntilSeconds = teleportType == LegacyVipTeleportType.SelfTeleport
                ? lastSeconds + 180
                : lastSeconds + 60;
            var remainingSeconds = Math.Max(0, waitUntilSeconds - nowSeconds);
            cooldownMessage = "You have to wait " +
                remainingSeconds.ToString(CultureInfo.InvariantCulture) +
                " more seconds to use the VIP Teleport.";
            return false;
        }

        updatedSession = session with
        {
            Character = WithCharacterAttribute(
                session.Character,
                key,
                nowSeconds.ToString(CultureInfo.InvariantCulture)),
        };
        return true;
    }

    private static async ValueTask SendLegacyVipTeamTeleportConfirmationsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyVipTeleportLocation location,
        CancellationToken cancellationToken)
    {
        var teamStates = context.TeamStates ?? new TeamSessionStateStore();
        var teamConnections = teamStates.GetConnections(context.Connection);
        if (teamConnections.Count <= 1)
        {
            return;
        }

        var frame = new LegacyVipHandlerPacket(
            LegacyVipTeleportType.TeammateConfirmation,
            location,
            Countdown: 15,
            Name: session.Character.Name).ToFrameBytes();
        var sentCount = 0;
        foreach (var connection in teamConnections)
        {
            if (ReferenceEquals(connection, context.Connection))
            {
                continue;
            }

            await connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sentCount++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyVipHandler team confirmations " +
                $"{FormatSession(context)} count={sentCount} location={location}.")
            .ConfigureAwait(false);
    }
}
