using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyTopHoursBattlePowerLimit = 480;
    private const int LegacyTopHoursPrizeConquerPoints = 500_000;
    private const long LegacyTopHoursConquerPointsWalletCap = 2_000_000_000;
    private const long LegacyTopHoursWinnerMapId = 1_002;
    private const long LegacyTopHoursWinnerX = 410;
    private const long LegacyTopHoursWinnerY = 355;
    private const ushort LegacyTopHoursEntryAvatarId = 63;
    private const ushort LegacyTopHoursExitAvatarId = 154;
    private const ushort LegacyTopHoursEndAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursEntryOptions =
    [
        new("Sign me up.", 1),
        new("I will come later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursClosedOptions =
    [
        new("I see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursExitOptions =
    [
        new("Yes", 1),
        new("Teleport to Twin City.", 2),
        new("Ah, wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursExitConfirmOptions =
    [
        new("Yes", 3),
        new("Ah, no.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursEndOptions =
    [
        new("Thanks", 58),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTopHoursRewardUnavailableOptions =
    [
        new("I understand.", LegacyNpcClosedOption),
    ];

    private static readonly IReadOnlyDictionary<uint, LegacyTopHoursEntry> LegacyTopHoursEntries =
        new Dictionary<uint, LegacyTopHoursEntry>
        {
            [19_941] = new(19_941, "NpcHandler.TopHoursxx1", "NpcHandler.cs:660-690", 1, 2, 10_397, 38, 37),
            [19_942] = new(19_942, "NpcHandler.TopHoursxx4", "NpcHandler.cs:774-804", 4, 6, 10_437, 45, 45),
            [19_943] = new(19_943, "NpcHandler.TopHoursxx8", "NpcHandler.cs:888-918", 8, 11, 10_398, 38, 38),
            [19_944] = new(19_944, "NpcHandler.TopHoursxx12", "NpcHandler.cs:1002-1032", 12, 15, 10_438, 45, 45),
            [19_945] = new(19_945, "NpcHandler.TopHoursxx17", "NpcHandler.cs:1116-1174", 17, 20, 10_399, 38, 38),
            [19_946] = new(19_946, "NpcHandler.TopHoursxx22", "NpcHandler.cs:1258-1316", 22, 25, 10_439, 45, 45),
            [19_947] = new(19_947, "NpcHandler.TopHoursxx27", "NpcHandler.cs:1400-1458", 27, 30, 10_401, 38, 38),
            [19_948] = new(19_948, "NpcHandler.TopHoursxx32", "NpcHandler.cs:1542-1600", 32, 35, 10_440, 38, 38),
            [19_949] = new(19_949, "NpcHandler.TopHoursxx37", "NpcHandler.cs:1684-1742", 37, 40, 10_402, 38, 38),
            [19_950] = new(19_950, "NpcHandler.TopHoursxx42", "NpcHandler.cs:1826-1884", 42, 45, 10_441, 45, 45),
            [19_951] = new(19_951, "NpcHandler.TopHoursxx47", "NpcHandler.cs:1968-2026", 47, 50, 10_403, 38, 38),
            [19_952] = new(19_952, "NpcHandler.TopHoursxx52", "NpcHandler.cs:2110-2168", 52, 55, 10_442, 45, 45),
            [19_953] = new(19_953, "NpcHandler.TopHoursxx57", "NpcHandler.cs:2252-2310", 56, 57, 10_404, 45, 45),
        };

    private static readonly IReadOnlyDictionary<uint, LegacyTopHoursExit> LegacyTopHoursExits =
        new Dictionary<uint, LegacyTopHoursExit>
        {
            [19_954] = new(19_954, "NpcHandler.TopHoursOutxx1", "NpcHandler.cs:693-771", 10_397, 4, 30),
            [19_955] = new(19_955, "NpcHandler.TopHoursOutxx4", "NpcHandler.cs:807-885", 10_437, 7, 30),
            [19_956] = new(19_956, "NpcHandler.TopHoursOutxx8", "NpcHandler.cs:921-999", 10_398, 12, 30),
            [19_957] = new(19_957, "NpcHandler.TopHoursOutxx12", "NpcHandler.cs:1035-1113", 10_438, 16, 30),
            [19_958] = new(19_958, "NpcHandler.TopHoursOutxx17", "NpcHandler.cs:1177-1255", 10_399, 21, 30),
            [19_959] = new(19_959, "NpcHandler.TopHoursOutxx22", "NpcHandler.cs:1319-1397", 10_439, 26, 45),
            [19_960] = new(19_960, "NpcHandler.TopHoursOutxx27", "NpcHandler.cs:1461-1539", 10_401, 31, 59),
            [19_961] = new(19_961, "NpcHandler.TopHoursOutxx32", "NpcHandler.cs:1603-1681", 10_440, 36, 59),
            [19_962] = new(19_962, "NpcHandler.TopHoursOutxx37", "NpcHandler.cs:1745-1823", 10_402, 41, 59),
            [19_963] = new(19_963, "NpcHandler.TopHoursOutxx42", "NpcHandler.cs:1887-1965", 10_441, 46, 59),
            [19_964] = new(19_964, "NpcHandler.TopHoursOutxx47", "NpcHandler.cs:2029-2107", 10_403, 51, 59),
            [19_965] = new(19_965, "NpcHandler.TopHoursOutxx52", "NpcHandler.cs:2171-2249", 10_442, 56, 59),
            [19_966] = new(19_966, "NpcHandler.TopHoursOutxx55", "NpcHandler.cs:2313-2391", 10_404, 58, 59),
        };

    internal static ValueTask<bool> TryHandleLegacyTopHoursNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return TryHandleLegacyTopHoursNpcAsync(context, session, npcId, optionId, DateTime.Now, cancellationToken);
    }

    internal static async ValueTask<bool> TryHandleLegacyTopHoursNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (LegacyTopHoursEntries.TryGetValue(npcId, out var entry))
        {
            return await TryHandleLegacyTopHoursEntryAsync(context, session, entry, optionId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        if (LegacyTopHoursExits.TryGetValue(npcId, out var exit))
        {
            return await TryHandleLegacyTopHoursExitAsync(context, session, exit, optionId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyTopHoursEntryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyTopHoursEntry entry,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        entry.SourceMethod,
                        [$"Hello! TopHours registration is open from {entry.StartMinuteText} through {entry.EndMinuteText}."],
                        LegacyTopHoursEntryOptions,
                        LegacyTopHoursEntryAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if ((context.ArenaStates?.GetStatus(context.Connection) ?? MsgArenaInfoStatus.NotSignedUp) !=
                    MsgArenaInfoStatus.NotSignedUp)
                {
                    await SendLegacyTopHoursSystemQualifierAsync(
                            context,
                            entry.SourceMethod,
                            entry.SourceLineRange,
                            optionId,
                            "You are already signed up for a qualifier arena. Leave that arena, then try again.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var battlePower = CalculateLegacyTopHoursBattlePower(session);
                if (battlePower >= LegacyTopHoursBattlePowerLimit)
                {
                    await SendLegacyTopHoursSystemQualifierAsync(
                            context,
                            entry.SourceMethod,
                            entry.SourceLineRange,
                            optionId,
                            "Sorry, this event is only for players under 480 Battle Power.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                if (entry.IsOpen(now.Minute))
                {
                    await SendLegacyNpcTeleportAsync(
                            context,
                            session,
                            entry.NpcId,
                            optionId,
                            new LegacyNpcTeleportDestination(entry.EventMapId, entry.X, entry.Y, entry.SourceMethod),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        entry.SourceMethod,
                        [$"Please sign up between {entry.StartMinuteText} and {entry.EndMinuteText}."],
                        LegacyTopHoursClosedOptions,
                        LegacyTopHoursEntryAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyTopHoursExitAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyTopHoursExit exit,
        byte optionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        exit.SourceMethod,
                        ["Hello, are you the last player alive?"],
                        LegacyTopHoursExitOptions,
                        LegacyTopHoursExitAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (!exit.IsRewardOpen(now.Minute))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            exit.SourceMethod,
                            ["TopHours ends at xx:59."],
                            LegacyTopHoursEndOptions,
                            LegacyTopHoursEndAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await TryClaimLegacyTopHoursWinnerRewardAsync(
                        context,
                        exit,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        exit.SourceMethod,
                        ["Do you want to teleport back to Twin City? Are you sure?"],
                        LegacyTopHoursExitConfirmOptions,
                        LegacyTopHoursExitAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        exit.NpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, exit.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryClaimLegacyTopHoursWinnerRewardAsync(
        WorldPacketHandlerContext context,
        LegacyTopHoursExit exit,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } sessions)
        {
            await SendLegacyTopHoursRewardUnavailableAsync(
                    context,
                    exit,
                    optionId,
                    "sessionsMissing",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var activeSessions = sessions.Snapshot();
        var alivePlayers = activeSessions
            .Where(active => IsLegacyTopHoursAliveOnMap(active.Session, exit.EventMapId))
            .ToArray();
        if (alivePlayers.Length != 1 ||
            !ReferenceEquals(alivePlayers[0].Connection, context.Connection))
        {
            await LogLegacyTopHoursRewardSkippedAsync(
                    context,
                    exit,
                    optionId,
                    alivePlayers.Length,
                    alivePlayers.Length == 1 ? "invokerIsNotWinner" : "soleAliveWinnerRequired")
                .ConfigureAwait(false);
            return;
        }

        var winner = alivePlayers[0];
        var winnerSession = winner.Session;
        var previousConquerPoints = winnerSession.Character.ConquerPoints;
        if (previousConquerPoints < 0 ||
            previousConquerPoints > LegacyTopHoursConquerPointsWalletCap)
        {
            await LogLegacyTopHoursRewardSkippedAsync(
                    context,
                    exit,
                    optionId,
                    alivePlayers.Length,
                    "conquerPointsOutOfRange")
                .ConfigureAwait(false);
            return;
        }

        var rewardedConquerPoints = previousConquerPoints + LegacyTopHoursPrizeConquerPoints;
        var overflowConquerPoints = Math.Max(0, rewardedConquerPoints - LegacyTopHoursConquerPointsWalletCap);
        var updatedConquerPoints = rewardedConquerPoints - overflowConquerPoints;
        var previousBankConquerPoints = winnerSession.Character.BankConquerPoints;
        if (previousBankConquerPoints < 0 ||
            previousBankConquerPoints > long.MaxValue - overflowConquerPoints)
        {
            await LogLegacyTopHoursRewardSkippedAsync(
                    context,
                    exit,
                    optionId,
                    alivePlayers.Length,
                    "bankConquerPointsOutOfRange")
                .ConfigureAwait(false);
            return;
        }

        var preflight = await PrepareLegacyTeleportAsync(
                context,
                winnerSession,
                new LegacyTeleportRequest(exit.SourceMethod, LegacyTopHoursWinnerMapId),
                cancellationToken)
            .ConfigureAwait(false);
        if (!preflight.Allowed)
        {
            return;
        }

        winnerSession = preflight.Session;

        var updatedBankConquerPoints = previousBankConquerPoints + overflowConquerPoints;
        var sourceCharacter = winnerSession.Character;
        var updatedAttributes = CopyCharacterAttributes(sourceCharacter.Attributes);
        updatedAttributes["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture);
        updatedAttributes["BankCps"] = updatedBankConquerPoints.ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = sourceCharacter with
        {
            ConquerPoints = updatedConquerPoints,
            BankConquerPoints = updatedBankConquerPoints,
            Attributes = updatedAttributes,
        };
        var persistedCharacter = ReduceLegacyTeleportDestination(
                context,
                updatedCharacter,
                new LegacyTeleportDestination(
                    LegacyTopHoursWinnerMapId,
                    LegacyTopHoursWinnerX,
                    LegacyTopHoursWinnerY))
            .FinalCharacter;
        var persistenceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = updatedConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["BankCps"] = updatedBankConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["Map"] = persistedCharacter.MapId.ToString(CultureInfo.InvariantCulture),
            ["X"] = persistedCharacter.X.ToString(CultureInfo.InvariantCulture),
            ["Y"] = persistedCharacter.Y.ToString(CultureInfo.InvariantCulture),
            ["PMap"] = persistedCharacter.PreviousMapId.ToString(CultureInfo.InvariantCulture),
            ["PMapX"] = persistedCharacter.PreviousMapX.ToString(CultureInfo.InvariantCulture),
            ["PMapY"] = persistedCharacter.PreviousMapY.ToString(CultureInfo.InvariantCulture),
            ["DynamicID"] = persistedCharacter.Attributes.TryGetValue("DynamicID", out var dynamicId)
                ? dynamicId
                : "0",
        };
        if (persistedCharacter.Attributes.TryGetValue("PDinamycID", out var previousDynamicId))
        {
            persistenceValues["PDinamycID"] = previousDynamicId;
        }

        updatedAttributes["Map"] = persistenceValues["Map"];
        updatedAttributes["X"] = persistenceValues["X"];
        updatedAttributes["Y"] = persistenceValues["Y"];
        updatedAttributes["PMap"] = persistenceValues["PMap"];
        updatedAttributes["PMapX"] = persistenceValues["PMapX"];
        updatedAttributes["PMapY"] = persistenceValues["PMapY"];

        var updatedSession = winnerSession with { Character = updatedCharacter };
        if (!TryPersistLegacyCharacterAttributes(
                updatedSession.UsersRoot,
                updatedSession.AccountId,
                updatedSession.Character.Uid,
                persistenceValues,
                out var path,
                out var reason))
        {
            await SendLegacyTopHoursRewardUnavailableAsync(
                    context,
                    exit,
                    optionId,
                    $"persistence:{reason}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(winner.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        if (overflowConquerPoints > 0)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(
                            $"{overflowConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                            "Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await BroadcastLegacyTopHoursWinnerRewardAsync(
                updatedContext,
                sessions,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        var teleportedSession = await SendLegacyTopHoursWinnerTeleportAsync(
                updatedContext,
                updatedSession,
                exit,
                optionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return;
        }

        updatedSession = teleportedSession;
        updatedContext = context with { Session = updatedSession };

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc TopHours winner reward " +
                $"{FormatSession(updatedContext)} source={exit.SourceMethod} lineRange={exit.SourceLineRange} " +
                $"option={optionId} map={exit.EventMapId.ToString(CultureInfo.InvariantCulture)} alivePlayers=1 " +
                $"conquerPoints={LegacyTopHoursPrizeConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"previousConquerPoints={previousConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedConquerPoints={updatedConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"overflowConquerPoints={overflowConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"previousBankCps={previousBankConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"updatedBankCps={updatedBankConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                $"path={Path.GetFileName(path)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> SendLegacyTopHoursWinnerTeleportAsync(
        WorldPacketHandlerContext updatedContext,
        WorldSession updatedSession,
        LegacyTopHoursExit exit,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var teleportedSession = await ExecuteLegacyTeleportDestinationAsync(
                updatedContext,
                updatedSession,
                new LegacyTeleportDestination(
                    LegacyTopHoursWinnerMapId,
                    LegacyTopHoursWinnerX,
                    LegacyTopHoursWinnerY),
                timestamp: 0,
                cancellationToken)
            .ConfigureAwait(false);
        if (teleportedSession is null)
        {
            return null;
        }

        await updatedContext.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc teleport transition " +
                $"{FormatSession(updatedContext with { Session = teleportedSession })} npcId={exit.NpcId} option={optionId} source={exit.SourceMethod} " +
                $"map={LegacyTopHoursWinnerMapId} x={LegacyTopHoursWinnerX} y={LegacyTopHoursWinnerY}.")
            .ConfigureAwait(false);
        return teleportedSession;
    }

    private static async ValueTask SendLegacyTopHoursRewardUnavailableAsync(
        WorldPacketHandlerContext context,
        LegacyTopHoursExit exit,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc TopHours " +
                $"{FormatSession(context)} source={exit.SourceMethod} lineRange={exit.SourceLineRange} " +
                $"option={optionId}; reason={reason}; durable CP persistence is required before session commit.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                exit.SourceMethod,
                ["TopHours winner rewards are not available yet."],
                LegacyTopHoursRewardUnavailableOptions,
                LegacyTopHoursEndAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsLegacyTopHoursAliveOnMap(WorldSession session, long mapId)
    {
        return session.Character.MapId == mapId && session.Character.MinHitPoints >= 1;
    }

    private static async ValueTask BroadcastLegacyTopHoursWinnerRewardAsync(
        WorldPacketHandlerContext context,
        WorldSessionStore sessions,
        WorldSession winner,
        CancellationToken cancellationToken)
    {
        var message =
            $" Congratulations {winner.Character.Name} claimed the prize [ " +
            $"{LegacyTopHoursPrizeConquerPoints.ToString(CultureInfo.InvariantCulture)} Cps ] " +
            "for winning the Top TopHours +   .";
        var frame = LegacyMsgMessagePacket.SystemCenter(message).ToFrameBytes();
        var sent = 0;
        foreach (var active in sessions.Snapshot())
        {
            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
            sent++;
        }

        await context.Log.WriteLineAsync(
                "WorldServer broadcast LegacyNpc TopHours winner reward " +
                $"{FormatSession(context)} winner={winner.Character.Name} count={sent.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyTopHoursRewardSkippedAsync(
        WorldPacketHandlerContext context,
        LegacyTopHoursExit exit,
        byte optionId,
        int alivePlayers,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "WorldServer skipped LegacyNpc TopHours winner reward " +
                $"{FormatSession(context)} source={exit.SourceMethod} lineRange={exit.SourceLineRange} " +
                $"option={optionId} map={exit.EventMapId.ToString(CultureInfo.InvariantCulture)} " +
                $"alivePlayers={alivePlayers.ToString(CultureInfo.InvariantCulture)} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static ulong CalculateLegacyTopHoursBattlePower(WorldSession session)
    {
        var character = session.Character;
        var battlePower = ReadAttributeUInt64(character.Attributes, "BattlePower", "BattlePowerValue");
        if (battlePower != 0)
        {
            return battlePower;
        }

        var guildBattlePower = ReadAttributeUInt64(character.Attributes, "GuildBattlePower");
        var rawExtraBattlePower = ReadAttributeUInt64(character.Attributes, "ExtraBattlePower");
        var extraBattlePower = rawExtraBattlePower > guildBattlePower ? rawExtraBattlePower - guildBattlePower : 0;
        var levelPower = Math.Max(0, character.Level + (character.Reborn * 5));
        return checked((ulong)levelPower +
            extraBattlePower +
            LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(session.PlayerItems, session.AlternateEquipmentActive));
    }

    private static async ValueTask SendLegacyTopHoursSystemQualifierAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        string sourceLineRange,
        byte optionId,
        string message,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemQualifier(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc TopHours qualifier message " +
                $"{FormatSession(context)} source={sourceMethod} lineRange={sourceLineRange} " +
                $"option={optionId} message={message}.")
            .ConfigureAwait(false);
    }

    private readonly record struct LegacyTopHoursEntry(
        uint NpcId,
        string SourceMethod,
        string SourceLineRange,
        int StartMinute,
        int EndMinute,
        long EventMapId,
        long X,
        long Y)
    {
        public string StartMinuteText => FormatLegacyTopHoursMinute(StartMinute);
        public string EndMinuteText => FormatLegacyTopHoursMinute(EndMinute);

        public bool IsOpen(int minute)
        {
            return minute >= StartMinute && minute <= EndMinute;
        }
    }

    private readonly record struct LegacyTopHoursExit(
        uint NpcId,
        string SourceMethod,
        string SourceLineRange,
        long EventMapId,
        int RewardStartMinute,
        int RewardEndMinute)
    {
        public bool IsRewardOpen(int minute)
        {
            return minute >= RewardStartMinute && minute <= RewardEndMinute;
        }
    }

    private static string FormatLegacyTopHoursMinute(int minute)
    {
        return $"xx:{minute:00}";
    }
}
