using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyChatViewDistance = 18;

    [PacketHandler(WorldPacketOpcodes.LegacyMsgMessage)]
    private static async ValueTask HandleLegacyMsgMessageAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.ProtocolProfile != ConquerProtocolProfile.VisibleDx8D13)
        {
            await context.Log.WriteLineAsync(
                    "PROTOCOL-PROFILE skipped LegacyMsgMessage inbound routing " +
                    $"profile={context.ProtocolProfile} opcode={frame.Header.Opcode} " +
                    "reason=only the VisibleDx8D13 wire is verified.")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyMsgMessagePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyMsgMessage packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyMsgMessage {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var message = packet.Content[3];
        if (message.StartsWith('@'))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyMsgMessage command {FormatSession(context)}; command routing is not modeled.")
                .ConfigureAwait(false);
            return;
        }

        var linkedItems = context.ChatItems?.CaptureLinks(
            session,
            message,
            context.WorldData?.ItemsById,
            context.TimeProvider) ?? 0;
        var outbound = NormalizeLegacyPlayerMessage(packet, session);
        var outboundFrame = outbound.ToFrameBytes();

        switch (packet.ChatType)
        {
            case LegacyMsgMessagePacket.TalkChatType:
                await SendLegacyTalkMessageAsync(context, session, outboundFrame, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyMsgMessagePacket.WhisperChatType:
                await SendLegacyWhisperMessageAsync(context, outbound, outboundFrame, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyMsgMessagePacket.WorldChatType:
                await SendLegacyWorldMessageAsync(context, outboundFrame, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyMsgMessage channel is not routed " +
                        $"{FormatSession(context)} chatType={packet.ChatType}.")
                    .ConfigureAwait(false);
                break;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyMsgMessage " +
                $"{FormatSession(context)} chatType={packet.ChatType} linkedItems={linkedItems}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTalkMessageAsync(
        WorldPacketHandlerContext context,
        WorldSession sender,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        var senderDynamicId = ReadCharacterDynamicId(sender.Character);
        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.MapId != sender.Character.MapId ||
                ReadCharacterDynamicId(active.Session.Character) != senderDynamicId ||
                LegacyGetDistance(
                    sender.Character.X,
                    sender.Character.Y,
                    active.Session.Character.X,
                    active.Session.Character.Y) > LegacyChatViewDistance)
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyWhisperMessageAsync(
        WorldPacketHandlerContext context,
        LegacyMsgMessagePacket packet,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        var targetName = packet.Content[1];
        var target = context.Sessions?.Snapshot().FirstOrDefault(active =>
            string.Equals(active.Session.Character.Name, targetName, StringComparison.Ordinal));
        if (target is not null)
        {
            await target.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk("The player is not online.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWorldMessageAsync(
        WorldPacketHandlerContext context,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (!ReferenceEquals(active.Connection, context.Connection))
            {
                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static LegacyMsgMessagePacket NormalizeLegacyPlayerMessage(
        LegacyMsgMessagePacket packet,
        WorldSession session)
    {
        var content = packet.Content.ToArray();
        content[0] = session.Character.Name;
        var transformation = ReadAttributeUInt64(session.Character.Attributes, "TransformationID");
        var mesh = checked(
            transformation * 10_000_000UL +
            checked((ulong)session.Character.Face * 10_000UL) +
            checked((ulong)session.Character.Body));

        return packet with
        {
            Mesh = checked((uint)mesh),
            Content = content,
        };
    }
}
