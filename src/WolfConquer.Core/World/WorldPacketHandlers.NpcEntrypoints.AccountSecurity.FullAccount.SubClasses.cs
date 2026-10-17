using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const long LegacyFullAccountSubClassCost = 100_000;
    private const byte LegacyFullAccountSubClassLevel = 9;
    private const byte LegacyFullAccountSubClassPhrase = 9;
    private const byte LegacyFullAccountApothecaryId = 5;
    private const string LegacyFullAccountSubClassEffect = "zf2-e300";
    private const string LegacyFullAccountSubClassInsufficientMessage =
        "You do not have enough conquer point.";
    private const string LegacyFullAccountSubClassAlreadyLearnedMessage =
        "Aha,~you~are~already~an~Apothecary,~remember?~I~suggest~you~focus~your~attention~on~upgrading~your~sub-class~level,~now.";

    private static readonly byte[] LegacyFullAccountSubClassIds =
    [
        1,
        2,
        3,
        4,
        5,
        9,
        6,
    ];

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountSubClassesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 22;
        var currentState = ReadLegacySubClassState(session.Character);
        if (currentState.Entries.FirstOrDefault(entry => entry.Id == LegacyFullAccountApothecaryId)?.Level >= 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecurityFullAccountSource,
                    [LegacyFullAccountSubClassAlreadyLearnedMessage],
                    [new LegacyNpcDialogOption("Oh.", LegacyNpcClosedOption)],
                    avatarId: 284,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints is < 0 or > uint.MaxValue)
        {
            await LogLegacyAccountSecurityFullAccountSubClassFenceAsync(
                    context,
                    "conquer-points-out-of-range")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints < LegacyFullAccountSubClassCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyAccountSecurityFullAccountSource,
                    [LegacyFullAccountSubClassInsufficientMessage],
                    [new LegacyNpcDialogOption("Aww!", LegacyNpcClosedOption)],
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountSubClassFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        var entries = currentState.Entries.ToList();
        var learned = new List<LegacySubClassEntry>(LegacyFullAccountSubClassIds.Length);
        foreach (var subClassId in LegacyFullAccountSubClassIds)
        {
            if (entries.Any(entry => entry.Id == subClassId))
            {
                continue;
            }

            var entry = new LegacySubClassEntry(
                subClassId,
                LegacyFullAccountSubClassLevel,
                LegacyFullAccountSubClassPhrase);
            entries.Add(entry);
            learned.Add(entry);
        }

        var updatedState = currentState with { Entries = entries.ToArray() };
        var updatedConquerPoints = checked(session.Character.ConquerPoints - LegacyFullAccountSubClassCost);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    session.Character with { ConquerPoints = updatedConquerPoints },
                    "ConquerPoints",
                    updatedConquerPoints.ToString(CultureInfo.InvariantCulture)),
                "SubProfInfo",
                FormatLegacySubClassState(updatedState)),
            "SubClassHashPoint",
            CalculateLegacySubClassHashPoint(updatedState).ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["SubProfInfo"] = updatedCharacter.Attributes["SubProfInfo"],
            ["SubClassHashPoint"] = updatedCharacter.Attributes["SubClassHashPoint"],
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityFullAccountSubClassFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in learned)
        {
            await context.Connection.SendAsync(
                    new LegacySubClassUpdatePacket(
                        LegacySubClassAction.LearnSubClass,
                        entry.Id,
                        entry.Level,
                        StudyReceive: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    LegacyFullAccountSubClassEffect).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount subclasses " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"cost={LegacyFullAccountSubClassCost} " +
                $"learned={string.Join(',', learned.Select(entry => entry.Id))} " +
                $"level={LegacyFullAccountSubClassLevel} phrase={LegacyFullAccountSubClassPhrase} " +
                "packetOrder=ConquerPoints,LearnSubClass*,Effect.")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyAccountSecurityFullAccountSubClassFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount subclasses " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=22 " +
            $"reason={reason}; no CP, subclass, session, or packet mutation was committed.");
    }
}
