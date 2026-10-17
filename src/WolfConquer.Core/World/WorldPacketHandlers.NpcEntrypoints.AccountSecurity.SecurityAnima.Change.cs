namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyAccountSecurityAnimaPinLengthMessage =
        "Please enter exactly six letters or digits.";
    private const string LegacyAccountSecurityAnimaOldPinIncorrectMessage =
        "The old PIN code is not correct.";

    private static async ValueTask HandleLegacyAccountSecurityAnimaOldPinAsync(
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
            if (!TrySetLegacyAccountSecurityAnimaFlowState(
                    context,
                    session,
                    session.LegacyAnimaSecurityPendingPin,
                    oldPinVerified: false,
                    out var rejectedSession))
            {
                await LogLegacyAccountSecurityAnimaPinFenceAsync(
                        context,
                        optionId: 11,
                        "session-store-missing")
                    .ConfigureAwait(false);
                return;
            }

            var rejectedContext = context with { Session = rejectedSession };
            await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                    rejectedContext,
                    LegacyAccountSecurityAnimaPinLengthMessage,
                    cancellationToken)
                .ConfigureAwait(false);
            await LegacyAccountSecurityHandleSecurityAnimaAsync(
                    rejectedContext,
                    rejectedSession,
                    npcId,
                    optionId: 10,
                    input: string.Empty,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var maskedPin = LegacyAccountSecurityMaskTail(input, visibleTailLength: 3);
        var currentPin = ReadAttributeString(
            session.Character.Attributes,
            LegacyAccountSecurityAnimaPinAttribute);
        var oldPinVerified = string.Equals(currentPin, input, StringComparison.OrdinalIgnoreCase);
        if (!TrySetLegacyAccountSecurityAnimaFlowState(
                context,
                session,
                maskedPin,
                oldPinVerified,
                out var stagedSession))
        {
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 11,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var stagedContext = context with { Session = stagedSession };
        if (oldPinVerified)
        {
            await SendLegacyAccountSecurityAnimaNewPinPromptAsync(
                    stagedContext,
                    stagedSession,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                stagedContext,
                LegacyAccountSecurityAnimaOldPinIncorrectMessage,
                cancellationToken)
            .ConfigureAwait(false);
        await LegacyAccountSecurityHandleSecurityAnimaAsync(
                stagedContext,
                stagedSession,
                npcId,
                optionId: 0,
                input: string.Empty,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyAccountSecurityAnimaNewPinPromptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog(
                    "Is this the old PIN code? " + session.LegacyAnimaSecurityPendingPin).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog("If correct, please enter the new PIN code.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput("New PIN:", optionId: 13, maxLength: 6).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(286).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc account-security input dialog " +
                $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"option=13 maxLength=6 wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyAccountSecurityChangeAnimaPinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        string input,
        CancellationToken cancellationToken)
    {
        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };
        if (!session.LegacyAnimaSecurityOldPinVerified)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer safety-fenced LegacyNpc AccountSecurity SecurityAnima direct PIN change " +
                    $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                    $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=13 " +
                    "reason=old-pin-not-verified; no PIN or session state was changed.")
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

        if (input.Length != 6)
        {
            await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                    context,
                    LegacyAccountSecurityAnimaPinLengthMessage,
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

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 13,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var normalizedPin = input.ToLowerInvariant();
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAccountSecurityAnimaPinAttribute,
            normalizedPin);
        var updatedSession = session with
        {
            Character = updatedCharacter,
            LegacyAnimaSecurityOldPinVerified = false,
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyAccountSecurityAnimaPinAttribute] = normalizedPin,
                },
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 13,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
                updatedContext,
                "Change completed",
                cancellationToken)
            .ConfigureAwait(false);
        await LegacyAccountSecurityHandleSecurityAnimaAsync(
                updatedContext,
                updatedSession,
                npcId,
                optionId: 0,
                input: string.Empty,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity SecurityAnima PIN change " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=13 " +
                "pinLength=6 storage=Character.PinCodeAnima normalized=lowercase " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode} " +
                "packetOrder=MessageBox,Finish,RootDialog.")
            .ConfigureAwait(false);
    }

    private static bool TrySetLegacyAccountSecurityAnimaFlowState(
        WorldPacketHandlerContext context,
        WorldSession session,
        string pendingPin,
        bool oldPinVerified,
        out WorldSession updatedSession)
    {
        updatedSession = session;
        if (context.Sessions is not { } sessions)
        {
            return false;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        updatedSession = session with
        {
            LegacyAnimaSecurityPendingPin = pendingPin,
            LegacyAnimaSecurityOldPinVerified = oldPinVerified,
        };
        sessions.Set(context.Connection, updatedSession);
        return true;
    }

    private static async ValueTask SendLegacyAccountSecurityAnimaMessageAndFinishAsync(
        WorldPacketHandlerContext context,
        string text,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
