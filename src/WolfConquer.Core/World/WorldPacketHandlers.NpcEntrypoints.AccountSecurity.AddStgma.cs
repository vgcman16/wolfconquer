using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyAccountSecurityAddStgmaCost = 500_000;
    private const int LegacyAccountSecurityAddStgmaDurationSeconds = 6_000;

    internal const string RuntimeLegacyStigmaExpiresAtUtcAttribute = "LegacyStigmaExpiresAtUtc";
    internal const string RuntimeLegacyMagicShieldExpiresAtUtcAttribute = "LegacyMagicShieldExpiresAtUtc";
    internal const string RuntimeLegacyStigmaRemoveOnDeadAttribute = "LegacyStigmaRemoveOnDead";
    internal const string RuntimeLegacyMagicShieldRemoveOnDeadAttribute = "LegacyMagicShieldRemoveOnDead";

    private static async ValueTask ApplyLegacyAccountSecurityAddStgmaAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityAddStgmaFenceAsync(context, "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints is < 0 or > uint.MaxValue)
        {
            await LogLegacyAccountSecurityAddStgmaFenceAsync(context, "conquer-points-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(
            session.Character.ConquerPoints - LegacyAccountSecurityAddStgmaCost);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(LegacyAccountSecurityAddStgmaDurationSeconds);
        var expiresAtText = expiresAt.ToString("O", CultureInfo.InvariantCulture);

        var stigmaCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        stigmaCharacter = WithLegacyStatusFlag(
            LegacyRootItemEffectFlagClock.ForgetProjection(stigmaCharacter, LegacyStigmaFlag), LegacyStigmaFlag);
        stigmaCharacter = WithCharacterAttribute(
            stigmaCharacter,
            RuntimeLegacyStigmaExpiresAtUtcAttribute,
            expiresAtText);
        stigmaCharacter = WithCharacterAttribute(
            stigmaCharacter,
            RuntimeLegacyStigmaRemoveOnDeadAttribute,
            bool.TrueString);

        var updatedCharacter = WithLegacyStatusFlag(
            LegacyRootItemEffectFlagClock.ForgetProjection(stigmaCharacter, LegacyMagicShieldFlag), LegacyMagicShieldFlag);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
            expiresAtText);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyMagicShieldRemoveOnDeadAttribute,
            bool.TrueString);

        var stigmaSession = session with { Character = stigmaCharacter };
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityAddStgmaFenceAsync(context, persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);

        // AddSpellFlag sends the full status vector once for each source call.
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context with { Session = stigmaSession },
                stigmaSession,
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity AddSTGMA mutation " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityAddStgmaSource} " +
                $"sourceLines={LegacyAccountSecurityAddStgmaSourceLines} optionId=1 " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"stigmaFlag={LegacyStigmaFlag} magicShieldFlag={LegacyMagicShieldFlag} " +
                $"durationSeconds={LegacyAccountSecurityAddStgmaDurationSeconds} " +
                "removeOnDead=true packetOrder=ConquerPoints,StigmaStatusFlags,MagicShieldStatusFlags; " +
                "source dialog advertises 600000 CPs/one hour while the executable branch deducts 500000 CPs/6000 seconds.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityAddStgmaFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity AddSTGMA mutation " +
            $"{FormatSession(context)} source={LegacyAccountSecurityAddStgmaSource} " +
            $"sourceLines={LegacyAccountSecurityAddStgmaSourceLines} optionId=1 " +
            $"reason={reason}; no CP, timed status, session state, or packet mutation was committed.");
    }
}
