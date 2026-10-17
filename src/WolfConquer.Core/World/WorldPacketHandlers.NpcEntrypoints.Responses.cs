using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask SendLegacyNpcDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        IReadOnlyList<string> texts,
        IReadOnlyList<LegacyNpcDialogOption> options,
        ushort? avatarId,
        CancellationToken cancellationToken)
    {
        foreach (var text in texts)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var option in options)
        {
            await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForOption(option.Text, option.OptionId).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        }

        if (avatarId.HasValue)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForAvatar(avatarId.Value).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyNpc dialog {FormatSession(context)} source={sourceMethod} " +
                $"texts={texts.Count} options={options.Count} " +
                $"avatar={(avatarId.HasValue ? avatarId.Value.ToString(CultureInfo.InvariantCulture) : "none")}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNpcOpenGuiActionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        bool useMapIdField,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var mapId = CheckedUInt32(character.MapId, nameof(character.MapId));
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            DwParam = LegacyNpcShopGuiId,
            Type = MsgActionType.OpenGuiNpc,
            NpcId = CheckedInt32(npcId, nameof(npcId)),
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
            DwParam3 = useMapIdField ? 0 : character.MapId,
            MapId = useMapIdField ? mapId : 0,
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc OpenGui action " +
                $"{FormatSession(context)} npcId={npcId} option={optionId} source={sourceMethod} " +
                $"gui={LegacyNpcShopGuiId} mapField={(useMapIdField ? "MapId" : "DwParam3")}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNpcOpenClientWindowActionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        uint windowId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            DwParam = windowId,
            Type = MsgActionType.OpenClientWindow,
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc OpenClientWindow action " +
                $"{FormatSession(context)} npcId={npcId} option={optionId} source={sourceMethod} window={windowId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyNpcTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyNpcTeleportDestination destination,
        CancellationToken cancellationToken,
        bool canTeleport = false,
        WorldSession? preflightSession = null)
    {
        var publicationSourceSession = session;
        if (context.Sessions?.TryGet(context.Connection, out var currentSession) == true &&
            currentSession is not null)
        {
            if (ReferenceEquals(currentSession, session) ||
                ReferenceEquals(currentSession, preflightSession) ||
                ReferenceEquals(currentSession, context.Session))
            {
                publicationSourceSession = currentSession;
            }
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                preflightSession ?? session,
                new LegacyTeleportRequest(
                    destination.SourceMethod,
                    destination.MapId,
                    CanTeleport: canTeleport),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        if (preflight.RemovedRide)
        {
            publicationSourceSession = preflight.Session;
        }

        session = CarryLegacyTeleportEntrySideEffects(preflight, session);

        var updatedSession = await ExecuteLegacyTeleportDestinationAsync(
                context with { Session = session },
                session,
                new LegacyTeleportDestination(
                    destination.MapId,
                    destination.X,
                    destination.Y),
                timestamp: 0,
                cancellationToken,
                expectedSession: publicationSourceSession)
            .ConfigureAwait(false);
        if (updatedSession is null)
        {
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc teleport transition " +
                $"{FormatSession(context with { Session = updatedSession })} npcId={npcId} option={optionId} source={destination.SourceMethod} " +
                $"map={destination.MapId} x={destination.X} y={destination.Y}.")
            .ConfigureAwait(false);
    }
}
