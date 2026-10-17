using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const byte LegacyAccountSecurityNameEditResetOptionId = 11;
    private const long LegacyAccountSecurityNameEditResetCost = 10_000;
    private const string LegacyAccountSecurityNameEditResetStateKey = "NameEditCount";
    private const string LegacyAccountSecurityNameEditResetSuccessText =
        "Your name reset was finished succesfully!";
    private const string LegacyAccountSecurityNameEditResetSuccessOptionText = "thank you";

    private static async ValueTask ApplyLegacyAccountSecurityNameEditResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (session.LegacyTradeActive)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity NameRegister reset " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityNameRegisterSource} " +
                    $"sourceLines={LegacyAccountSecurityNameRegisterSourceLines} " +
                    $"optionId={LegacyAccountSecurityNameEditResetOptionId} " +
                    "reason=trade-active; source branch is intentionally silent.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityNameEditResetFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints is < 0 or > uint.MaxValue)
        {
            await LogLegacyAccountSecurityNameEditResetFenceAsync(
                    context,
                    "conquer-points-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(
            session.Character.ConquerPoints - LegacyAccountSecurityNameEditResetCost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            LegacyAccountSecurityNameEditResetStateKey,
            0UL);
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
            [LegacyAccountSecurityNameEditResetStateKey] = "0",
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityNameEditResetFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyAccountSecurityNameRegisterSource,
                [LegacyAccountSecurityNameEditResetSuccessText],
                [new LegacyNpcDialogOption(
                    LegacyAccountSecurityNameEditResetSuccessOptionText,
                    LegacyNpcClosedOption)],
                avatarId: 31,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity NameRegister reset " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityNameRegisterSource} " +
                $"sourceLines={LegacyAccountSecurityNameRegisterSourceLines} " +
                $"optionId={LegacyAccountSecurityNameEditResetOptionId} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"stateKey={LegacyAccountSecurityNameEditResetStateKey} nameEditCount=0 " +
                "packetOrder=ConquerPoints,NpcDialog,NpcOption,NpcAvatar,NpcFinish.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityNameEditResetFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity NameRegister reset " +
            $"{FormatSession(context)} source={LegacyAccountSecurityNameRegisterSource} " +
            $"sourceLines={LegacyAccountSecurityNameRegisterSourceLines} " +
            $"optionId={LegacyAccountSecurityNameEditResetOptionId} " +
            $"reason={reason}; no CP, name-edit count, session state, or packets were committed.");
    }
}
