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
    private const string RuntimeThrillingSpookMessageBox = "ThrillingSpook";
    private const string RuntimePendingMessageBoxDynamicIdAttribute = LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute;

    private const long LegacyThrillingSpookSourceMapId = 1_036;
    private const long LegacyThrillingSpookSourceX = 247;
    private const long LegacyThrillingSpookSourceY = 144;
    private const long LegacyThrillingSpookTargetMapId = 2_090;
    private const long LegacyThrillingSpookTargetX = 32;
    private const long LegacyThrillingSpookTargetY = 30;
    private const string LegacyThrillingSpookPrompt =
        "A~Thrilling~Spook~is~surprisingly~strong.~You`d~better~team~up~and~try~to~kill~it~with~a~few~others!";

    private static long s_legacyThrillingSpookDynamicId = 2_090_000;

    [PacketHandler(WorldPacketOpcodes.LegacyMapTraps)]
    private static async ValueTask HandleLegacyMapTrapsAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (frame.Payload.Length != 0)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMapTraps packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyMapTraps {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        if (character.MapId != LegacyThrillingSpookSourceMapId ||
            LegacyGetDistance(
                character.X,
                character.Y,
                LegacyThrillingSpookSourceX,
                LegacyThrillingSpookSourceY) >= LegacyViewThreshold)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMapTraps ThrillingSpook prompt " +
                    $"{FormatSession(context)} map={character.MapId} x={character.X} y={character.Y}.")
                .ConfigureAwait(false);
            return;
        }

        var dynamicId = Interlocked.Increment(ref s_legacyThrillingSpookDynamicId);
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimePendingMessageBoxAttribute] = RuntimeThrillingSpookMessageBox,
            [RuntimePendingMessageBoxMapIdAttribute] = LegacyThrillingSpookTargetMapId.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxXAttribute] = LegacyThrillingSpookTargetX.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxYAttribute] = LegacyThrillingSpookTargetY.ToString(CultureInfo.InvariantCulture),
            [RuntimePendingMessageBoxDynamicIdAttribute] = dynamicId.ToString(CultureInfo.InvariantCulture),
        };
        context.Sessions?.Set(
            context.Connection,
            session with { Character = character with { Attributes = attributes } });

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyThrillingSpookPrompt).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyMapTraps ThrillingSpook message box " +
                $"{FormatSession(context)} dynamicId={dynamicId}.")
            .ConfigureAwait(false);
    }


    private static async ValueTask HandleThrillingSpookMessageBoxReplyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyNpcDialogRequestPacket packet)
    {
        var sourceCharacter = session.Character;
        var hasDestination = TryReadPendingMessageBoxDestination(
            sourceCharacter.Attributes,
            out var mapId,
            out var x,
            out var y);
        var hasDynamicId = TryReadInt64Attribute(
            sourceCharacter.Attributes,
            RuntimePendingMessageBoxDynamicIdAttribute,
            out var dynamicId);
        var clearedCharacter = WithoutRuntimeMessageBoxAttributes(sourceCharacter);

        if (packet.IsAcceptedMessageBox && hasDestination && hasDynamicId)
        {
            var teleported = TeleportThrillingSpookParticipants(
                context,
                session,
                clearedCharacter,
                mapId,
                x,
                y,
                dynamicId);

            await context.Log.WriteLineAsync(
                    "WorldServer accepted LegacyMapTraps ThrillingSpook message box " +
                    $"{FormatSession(context)} map={mapId} x={x} y={y} dynamicId={dynamicId} count={teleported}.")
                .ConfigureAwait(false);
            return;
        }

        context.Sessions?.Set(context.Connection, session with { Character = clearedCharacter });
        if (packet.IsAcceptedMessageBox)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyMapTraps ThrillingSpook teleport " +
                    $"{FormatSession(context)}; destination attributes are missing.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer canceled LegacyMapTraps ThrillingSpook message box " +
                $"{FormatSession(context)} option={packet.OptionId}.")
            .ConfigureAwait(false);
    }

    private static int TeleportThrillingSpookParticipants(
        WorldPacketHandlerContext context,
        WorldSession sourceSession,
        CharacterSnapshot clearedSourceCharacter,
        long mapId,
        long x,
        long y,
        long dynamicId)
    {
        if (context.Sessions is null)
        {
            return 0;
        }

        var count = 0;
        var connections = context.TeamStates?.GetConnections(context.Connection);
        if (connections is null || connections.Count == 0)
        {
            connections = [context.Connection];
        }

        foreach (var connection in connections)
        {
            if (!context.Sessions.TryGet(connection, out var targetSession) ||
                targetSession is null)
            {
                continue;
            }

            var targetCharacter = ReferenceEquals(connection, context.Connection)
                ? clearedSourceCharacter
                : targetSession.Character;
            if (!ReferenceEquals(connection, context.Connection) &&
                LegacyGetDistance(
                    sourceSession.Character.X,
                    sourceSession.Character.Y,
                    targetCharacter.X,
                    targetCharacter.Y) > LegacyViewThreshold)
            {
                continue;
            }

            var updatedCharacter = WithLegacyDynamicMapTeleport(
                targetCharacter,
                mapId,
                x,
                y,
                dynamicId);
            context.Sessions.Set(connection, targetSession with { Character = updatedCharacter });
            count++;
        }

        return count;
    }

}
