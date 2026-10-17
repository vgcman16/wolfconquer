using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyFullAccountCpGiftThreshold = 199_999_999;
    private const long LegacyFullAccountCpGiftAmount = 20_000_000;
    private const string LegacyFullAccountCpGiftCapMessage = "Error...";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountCpGiftAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 18;
        if (session.Character.ConquerPoints is < 0 or > uint.MaxValue)
        {
            await LogLegacyAccountSecurityFullAccountCpGiftFenceAsync(
                    context,
                    "conquer-points-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints >= LegacyFullAccountCpGiftThreshold)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket.ForMessageBox(LegacyFullAccountCpGiftCapMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer rejected LegacyNpc AccountSecurity FullAccount CP gift " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    $"conquerPoints={session.Character.ConquerPoints} " +
                    $"threshold={LegacyFullAccountCpGiftThreshold} message={LegacyFullAccountCpGiftCapMessage}")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountCpGiftFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(
            session.Character.ConquerPoints + LegacyFullAccountCpGiftAmount);
        var updatedCharacter = WithCharacterAttribute(
            session.Character with { ConquerPoints = updatedConquerPoints },
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
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
            await LogLegacyAccountSecurityFullAccountCpGiftFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount CP gift " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"gift={LegacyFullAccountCpGiftAmount} threshold={LegacyFullAccountCpGiftThreshold} " +
                "packetOrder=ConquerPoints.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityFullAccountCpGiftFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount CP gift " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=18 " +
            $"reason={reason}; no CP, session state, or packet mutation was committed.");
    }
}
