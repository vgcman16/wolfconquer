using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleSetPkModeActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction SetPkMode; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var previousPkMode = ReadAttributeUInt64(session.Character.Attributes, RuntimePkModeAttribute, "PKMode") & byte.MaxValue;
        if (previousPkMode == LegacyPkModeJiang)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You have temporarily quit the Jiang Hu!").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var requestedPkMode = packet.DwParam & byte.MaxValue;
        var updatedCharacter = WithCharacterAttribute(session.Character, RuntimePkModeAttribute, requestedPkMode);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(
                (packet with { DwParam3 = packet.DwParam }).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (TryGetLegacyPkModeMessage(requestedPkMode, out var message))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction SetPkMode " +
                $"{FormatSession(updatedContext)} pkMode={requestedPkMode} requestedDwParam={packet.DwParam}; " +
                "source=MsgDataPacket.SetPkMode [Data(96)] stores Player.PkMode, sets dwParam3=dwParam, and echoes ActionCreate(data).")
            .ConfigureAwait(false);
    }

    private static bool TryGetLegacyPkModeMessage(uint pkMode, out string message)
    {
        message = pkMode switch
        {
            LegacyPkModePk => "Free PK mode. You can attack monster and all players.",
            LegacyPkModeCapture => "Capture PK mode. You can only attack monsters, black-name and blue-name players.",
            LegacyPkModePeace => "Peace mode. You can only attack monsters.",
            LegacyPkModeTeam => "Team PK mode. You can attack monster and all players except your teammates or guild.",
            LegacyPkModeRevenge => "Revenge PK mode. You can attack monster and all Your Enemy List Players.",
            LegacyPkModeGuild => "Guild PK mode. You can attack monster and all Your Guild's Enemies",
            _ => string.Empty,
        };
        return message.Length != 0;
    }
}
