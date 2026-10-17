using System.Data.Common;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyAccountSecurityAnimaEmailRecoveryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string input,
        CancellationToken cancellationToken)
    {
        if (input.Length == 0)
        {
            return;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };
        var matches = await TryMatchLegacyAccountSecurityAnimaEmailAsync(
                context,
                session,
                optionId: 6,
                NormalizeLegacyAccountSecurityAnimaEmail(input),
                cancellationToken)
            .ConfigureAwait(false);
        if (!matches.HasValue)
        {
            return;
        }

        if (matches.Value)
        {
            var pin = ReadAttributeString(
                session.Character.Attributes,
                LegacyAccountSecurityAnimaPinAttribute);
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecuritySecurityAnimaSource,
                    ["Your PIN code [ " + pin + " ]"],
                    [new LegacyNpcDialogOption("Just passing by.", LegacyNpcClosedOption)],
                    avatarId: 86,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecuritySecurityAnimaSource,
                    ["Please re-enter your data."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    avatarId: 86,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity SecurityAnima email recovery " +
                $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=6 " +
                $"emailMatched={matches.Value.ToString().ToLowerInvariant()} " +
                $"pinRevealed={matches.Value.ToString().ToLowerInvariant()} " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyAccountSecurityAnimaEmailRemovalAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        string input,
        CancellationToken cancellationToken)
    {
        if (input.Length == 0)
        {
            return;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };
        var matches = await TryMatchLegacyAccountSecurityAnimaEmailAsync(
                context,
                session,
                optionId: 60,
                NormalizeLegacyAccountSecurityAnimaEmail(input),
                cancellationToken)
            .ConfigureAwait(false);
        if (!matches.HasValue)
        {
            return;
        }

        if (!matches.Value)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecuritySecurityAnimaSource,
                    ["Please re-enter your data."],
                    [new LegacyNpcDialogOption("Okay.", 5)],
                    avatarId: 86,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer applied LegacyNpc AccountSecurity SecurityAnima email removal check " +
                    $"{FormatSession(context)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                    $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=60 " +
                    $"emailMatched=false wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 60,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyAccountSecurityAnimaPinAttribute,
            string.Empty);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            LegacyAccountSecurityAnimaActivatedAttribute,
            "false");
        var updatedSession = session with { Character = updatedCharacter };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyAccountSecurityAnimaPinAttribute] = string.Empty,
                },
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityAnimaPinFenceAsync(
                    context,
                    optionId: 60,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyAccountSecuritySecurityAnimaSource,
                ["Successfully removed PIN code."],
                [new LegacyNpcDialogOption("Just looking.", 20)],
                avatarId: 86,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity SecurityAnima email removal " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecuritySecurityAnimaSource} " +
                $"sourceLines={LegacyAccountSecuritySecurityAnimaSourceLines} optionId=60 " +
                "emailMatched=true pinCleared=true activated=false persistence=character-snapshot " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode} " +
                "packetOrder=Dialog,Option,Avatar,Finish.")
            .ConfigureAwait(false);
    }

    private static string NormalizeLegacyAccountSecurityAnimaEmail(string input)
    {
        return input.Contains('@', StringComparison.Ordinal)
            ? input
            : input + "@yahoo.com";
    }

    private static async ValueTask<bool?> TryMatchLegacyAccountSecurityAnimaEmailAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string email,
        CancellationToken cancellationToken)
    {
        if (context.AccountEmailVerifier is not { } verifier)
        {
            await LegacyAccountSecuritySendUnavailableAsync(
                    context,
                    LegacyAccountSecuritySecurityAnimaSource,
                    LegacyAccountSecuritySecurityAnimaSourceLines,
                    LegacyAccountSecuritySecurityAnimaNpcId,
                    optionId,
                    "source account-email authority is unavailable",
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }

        try
        {
            return await verifier.MatchesEmailAsync(session.AccountId, email, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is DbException or IOException or InvalidOperationException or TimeoutException)
        {
            await LegacyAccountSecuritySendUnavailableAsync(
                    context,
                    LegacyAccountSecuritySecurityAnimaSource,
                    LegacyAccountSecuritySecurityAnimaSourceLines,
                    LegacyAccountSecuritySecurityAnimaNpcId,
                    optionId,
                    "account-email authority failed: " + ex.GetType().Name,
                    cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
    }
}
