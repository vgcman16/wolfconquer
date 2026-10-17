namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyAccountSecurityAnimaActivatedAttribute = "TREPIN2";

    private static async ValueTask ApplyLegacyAccountSecurityActivateAnimaPinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        string input,
        CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        if (input.Length != 6)
        {
            await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                    context,
                    LegacyAccountSecurityAnimaOldPinIncorrectMessage,
                    cancellationToken)
                .ConfigureAwait(false);
            await LegacyAccountSecurityHandleSecurityAnimaAsync(
                    context,
                    session,
                    npcId,
                    optionId: 0,
                    input: string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var currentPin = ReadAttributeString(
            session.Character.Attributes,
            LegacyAccountSecurityAnimaPinAttribute);
        if (!string.Equals(currentPin, input, StringComparison.OrdinalIgnoreCase))
        {
            await LegacyAccountSecurityHandleSecurityAnimaAsync(
                    context,
                    session,
                    npcId,
                    optionId: 0,
                    input: string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc AccountSecurity SecurityAnima activation " +
                    $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                    $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=80 " +
                    "reason=session-store-missing; no activation state or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAccountSecurityAnimaActivatedAttribute,
            "true");
        var updatedSession = session with { Character = updatedCharacter };
        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                updatedContext,
                "Account activated",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity SecurityAnima activation " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=80 " +
                "activated=true persistence=transient-source " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode} " +
                "packetOrder=MessageBox,Finish.")
            .ConfigureAwait(false);
    }
}
