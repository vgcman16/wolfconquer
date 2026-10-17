using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyAccountSecurityPkResetEffect = "eidolon";
    private const string LegacyAccountSecurityPkResetFiveHundredMessage =
        "You have deducted 500 PK points.";

    private static async ValueTask ApplyLegacyAccountSecurityPkResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var (conquerPointCost, pkPointReduction, messageBeforeEffect, successMessage) = optionId switch
        {
            1 => (2_000_000L, 100L, false, "....#55"),
            2 => (5_000_000L, 500L, true, LegacyAccountSecurityPkResetFiveHundredMessage),
            3 => (5_000L, 500L, true, LegacyAccountSecurityPkResetFiveHundredMessage),
            _ => throw new ArgumentOutOfRangeException(nameof(optionId)),
        };

        // The source evaluates balance/PK prerequisites before silently rejecting an active trade.
        if (session.LegacyTradeActive)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity PkReset mutation " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityPkResetSource} " +
                    $"sourceLines={LegacyAccountSecurityPkResetSourceLines} optionId={optionId} " +
                    "reason=trade-active; source branch is intentionally silent.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityPkResetFenceAsync(
                    context,
                    optionId,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Uid is < 0 or > uint.MaxValue ||
            session.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            session.Character.PkPoints is < 0 or > ushort.MaxValue)
        {
            await LogLegacyAccountSecurityPkResetFenceAsync(
                    context,
                    optionId,
                    "character-scalar-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(session.Character.ConquerPoints - conquerPointCost);
        var updatedPkPoints = checked(session.Character.PkPoints - pkPointReduction);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = updatedConquerPoints,
            PkPoints = updatedPkPoints,
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "PKPoints",
            checked((ulong)updatedPkPoints));
        updatedCharacter = ApplyLegacyAccountSecurityPkNameFlags(updatedCharacter, updatedPkPoints);

        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["PKPoints"] = updatedPkPoints.ToString(CultureInfo.InvariantCulture),
        };
        if (updatedCharacter.Attributes.TryGetValue("StatusFlags", out var statusFlags))
        {
            characterValues["StatusFlags"] = statusFlags;
        }
        else if (updatedCharacter.Attributes.TryGetValue("BitVector", out var bitVector))
        {
            characterValues["BitVector"] = bitVector;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityPkResetFenceAsync(
                    context,
                    optionId,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyPKPointsStartupUpdate(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);

        var effectFrame = LegacyStringPacket
            .PlayerEffect(checked((uint)updatedCharacter.Uid), LegacyAccountSecurityPkResetEffect)
            .ToFrameBytes();
        var messageFrame = LegacyNpcReplyPacket.ForMessageBox(successMessage).ToFrameBytes();
        if (messageBeforeEffect)
        {
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
            await SendFrameToPlayerAndVisiblePeersAsync(
                    updatedContext,
                    context.Connection,
                    updatedSession,
                    effectFrame,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await SendFrameToPlayerAndVisiblePeersAsync(
                    updatedContext,
                    context.Connection,
                    updatedSession,
                    effectFrame,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(messageFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity PkReset mutation " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityPkResetSource} " +
                $"sourceLines={LegacyAccountSecurityPkResetSourceLines} optionId={optionId} " +
                $"pkPoints={session.Character.PkPoints}->{updatedPkPoints} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"effect={LegacyAccountSecurityPkResetEffect} packetOrder=" +
                (messageBeforeEffect
                    ? "StatusFlags,PKPoints,ConquerPoints,MessageBox,Effect"
                    : "StatusFlags,PKPoints,ConquerPoints,Effect,MessageBox") + ".")
            .ConfigureAwait(false);
    }

    private static CharacterSnapshot ApplyLegacyAccountSecurityPkNameFlags(
        CharacterSnapshot character,
        long pkPoints)
    {
        character = WithoutLegacyStatusFlag(character, LegacyRedNameStatusFlag);
        character = WithoutLegacyStatusFlag(character, LegacyBlackNameStatusFlag);

        return pkPoints switch
        {
            > 99 => WithLegacyStatusFlag(character, LegacyBlackNameStatusFlag),
            > 29 => WithLegacyStatusFlag(character, LegacyRedNameStatusFlag),
            _ => character,
        };
    }

    private static Task LogLegacyAccountSecurityPkResetFenceAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity PkReset mutation " +
            $"{FormatSession(context)} source={LegacyAccountSecurityPkResetSource} " +
            $"sourceLines={LegacyAccountSecurityPkResetSourceLines} optionId={optionId} " +
            $"reason={reason}; no PK points, CPs, status flags, session state, or packets were committed.");
    }
}
