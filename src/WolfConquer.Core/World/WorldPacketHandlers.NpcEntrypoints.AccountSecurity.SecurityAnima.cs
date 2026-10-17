namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyAccountSecurityAnimaPinAttribute = "PinCodeAnima";

    private static bool LegacyAccountSecurityTryStageAnimaPin(
        WorldPacketHandlerContext context,
        WorldSession session,
        string input,
        out WorldSession stagedSession)
    {
        stagedSession = session;
        if (context.Sessions is not { } sessions)
        {
            return false;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);

        stagedSession = session with
        {
            LegacyAnimaSecurityPendingPin = input,
            LegacyAnimaSecurityOldPinVerified = false,
        };
        sessions.Set(context.Connection, stagedSession);
        return true;
    }

    private static async ValueTask ApplyLegacyAccountSecurityCreateAnimaPinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        CancellationToken cancellationToken)
    {
        var currentSession = LegacyAccountSecurityResolveAnimaSession(context, session);
        if (!ReferenceEquals(currentSession, session))
        {
            session = currentSession;
            context = context with { Session = currentSession };
        }

        var currentPin = ReadAttributeString(
            session.Character.Attributes,
            LegacyAccountSecurityAnimaPinAttribute);
        var pendingPin = session.LegacyAnimaSecurityPendingPin;
        if (currentPin.Length != 0 || pendingPin.Length != 6)
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
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 3,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var normalizedPin = pendingPin.ToLowerInvariant();
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAccountSecurityAnimaPinAttribute,
            normalizedPin);
        var updatedSession = session with { Character = updatedCharacter };

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
                    optionId: 3,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await updatedContext.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox("Completed " + pendingPin).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await updatedContext.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(286).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await updatedContext.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity SecurityAnima PIN creation " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=3 " +
                "pinLength=6 storage=Character.PinCodeAnima normalized=lowercase " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode} " +
                "packetOrder=MessageBox,Avatar,Finish.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityAnimaPinFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity SecurityAnima PIN mutation " +
            $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
            $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId={optionId} " +
            $"reason={reason}; no PIN, session state, or packets were committed.");
    }

    private static WorldSession LegacyAccountSecurityResolveAnimaSession(
        WorldPacketHandlerContext context,
        WorldSession session)
    {
        return context.Sessions is { } sessions &&
            sessions.TryGet(context.Connection, out var storedSession) &&
            storedSession is not null
            ? storedSession
            : session;
    }
}
