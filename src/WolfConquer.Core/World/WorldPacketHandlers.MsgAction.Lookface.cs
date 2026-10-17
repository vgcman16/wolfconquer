namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyChangeLookfaceCost = 500;
    private const string LegacyChangeLookfaceNoMoneyMessage = "You do not have 500 silvers with you.";

    private static async ValueTask HandleChangeLookfaceAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction ChangeLookface {FormatSession(context)}; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        if (ReadAttributeUInt64(session.Character.Attributes, "PokerTableID", "PokerTableId") != 0 ||
            ReadAttributeUInt64(session.Character.Attributes, "PokerSeat") != 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction ChangeLookface " +
                    $"{FormatSession(context)}; source=MsgDataPacket.ChangeLookface [Data(151)] returns while in poker.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Money < LegacyChangeLookfaceCost)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyChangeLookfaceNoMoneyMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer rejected MsgAction ChangeLookface " +
                    $"{FormatSession(context)} money={session.Character.Money}; legacy requires 500 silver.")
                .ConfigureAwait(false);
            return;
        }

        var normalizedFace = NormalizeLegacyLookface(session.Character.Body, packet.DwParam);
        var updatedMoney = checked(session.Character.Money - LegacyChangeLookfaceCost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with
            {
                Face = normalizedFace,
                Money = updatedMoney,
            },
            "Face",
            normalizedFace);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.Money,
                CheckedUInt64(updatedMoney, nameof(updatedMoney)),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction ChangeLookface " +
                $"{FormatSession(updatedContext)} requestedFace={packet.DwParam} face={normalizedFace} " +
                $"body={updatedCharacter.Body} money={updatedMoney}; " +
                "source=MsgDataPacket.ChangeLookface [Data(151)] normalizes Player.Face, charges 500 silver, " +
                "echoes ActionCreate(data), and sends Money update.")
            .ConfigureAwait(false);
    }

    private static ushort NormalizeLegacyLookface(long body, uint requestedFace)
    {
        if (body > 2_000)
        {
            requestedFace = requestedFace < 200 ? requestedFace + 200 : requestedFace;
        }
        else
        {
            requestedFace = requestedFace > 200 ? requestedFace - 200 : requestedFace;
        }

        return unchecked((ushort)requestedFace);
    }
}
