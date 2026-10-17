using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyClassPkWarAvatarId = 154;
    private const string LegacyClassPkWarSourceMethod = "NpcHandler.ClassPkWar";
    private const string LegacyClassPkEnvoySourceMethod = "NpcHandler.ClassPkEnvoy";

    private static readonly LegacyNpcDialogOption[] LegacyClassPkEnvoyRootOptions =
    [
        new("Tell me more.", 1),
        new("Sign up.", 2),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkEnvoyUnavailableOptions =
    [
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkWarRootOptions =
    [
        new("Yes", 1),
        new("Teleport to Twin City.", 2),
        new("Ah, wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkWarReturnOptions =
    [
        new("Yes", 3),
        new("Ah no.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPkWarUnavailableOptions =
    [
        new("Ah ok", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyClassPkWarNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyClassPkEnvoyNpcId => await TryHandleLegacyClassPkEnvoyAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyClassPkWarNpcId => await TryHandleLegacyClassPkWarArenaNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyClassPkEnvoyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyClassPkEnvoySourceMethod,
                        ["Everyone settle down! I need to announce that there are new rules for the Class PK War!"],
                        LegacyClassPkEnvoyRootOptions,
                        LegacyClassPkWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await context.Log.WriteLineAsync(
                        "WorldServer handled source no-op LegacyNpc ClassPkEnvoy " +
                        $"{FormatSession(context)} source={LegacyClassPkEnvoySourceMethod} option={optionId}.")
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryEnterLegacyClassPkWarAsync(context, session, optionId, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyClassPkWarArenaNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyClassPkWarSourceMethod,
                        ["Hello, are you the last player alive?"],
                        LegacyClassPkWarRootOptions,
                        LegacyClassPkWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await TryClaimLegacyClassPkWarRewardAsync(
                        context,
                        session,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyClassPkWarSourceMethod,
                        ["You want to teleport back to Twin City? Are you sure?"],
                        LegacyClassPkWarReturnOptions,
                        LegacyClassPkWarAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyClassPkWarNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_002, 410, 354, LegacyClassPkWarSourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask TryEnterLegacyClassPkWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacyClassPkWarRuntimeState(session.Character);
        if (!state.Known)
        {
            await SendLegacyClassPkEnvoyUnavailableAsync(
                    context,
                    optionId,
                    "ClassPkWar runtime process/finish timer is not locally represented for this character",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!state.BracketIsValid)
        {
            await SendLegacyClassPkEnvoyUnavailableAsync(
                    context,
                    optionId,
                    $"character class/level did not map to a modeled Class PK bracket type={state.TournamentType} level={state.TournamentLevel}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var join = LegacyClassPkWarState.CheckJoin(
            state.Process,
            state.FinishTimer,
            DateTimeOffset.UtcNow,
            state.DynamicId);
        if (!join.CanJoin)
        {
            await SendLegacyClassPkEnvoyUnavailableAsync(
                    context,
                    optionId,
                    $"source CheckJoin denied entry process={state.Process} finishTimer={state.FinishTimer:O}",
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcTeleportAsync(
                context,
                session,
                LegacyClassPkEnvoyNpcId,
                optionId,
                new LegacyNpcTeleportDestination(
                    LegacyClassPkWarState.MapId,
                    81,
                    62,
                    LegacyClassPkEnvoySourceMethod),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc ClassPkEnvoy enter " +
                $"{FormatSession(context)} source={LegacyClassPkEnvoySourceMethod} option={optionId} " +
                $"type={state.TournamentType} level={state.TournamentLevel} dynamicId={state.DynamicId}.")
            .ConfigureAwait(false);

        if (state.DynamicId != 0)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc ClassPkEnvoy dynamic instance " +
                    $"{FormatSession(context)} source={LegacyClassPkEnvoySourceMethod} option={optionId}; " +
                    "source teleports with war.DinamicID, while the shared local NPC teleport transition currently stores DynamicID=0.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyClassPkEnvoyUnavailableAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyClassPkEnvoySourceMethod,
                ["I'm sorry, but the Class PK War is only held on Mondays."],
                LegacyClassPkEnvoyUnavailableOptions,
                LegacyClassPkWarAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ClassPkEnvoy unavailable " +
                $"{FormatSession(context)} source={LegacyClassPkEnvoySourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyClassPkWarRewardFencedAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyClassPkWarSourceMethod,
                ["Sorry, the war is not finished, or other players are still alive."],
                LegacyClassPkWarUnavailableOptions,
                LegacyClassPkWarAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ClassPkWar reward fenced " +
                $"{FormatSession(context)} source={LegacyClassPkWarSourceMethod} option={optionId}; " +
                $"{reason}; source reward is {LegacyClassPkWarState.RewardConquerPoints.ToString(CultureInfo.InvariantCulture)} CP " +
                $"and PowerExpBall stack {LegacyClassPkWarState.RewardPowerExpBallStack.ToString(CultureInfo.InvariantCulture)}.")
            .ConfigureAwait(false);
    }

    private static LegacyClassPkWarRuntimeState ReadLegacyClassPkWarRuntimeState(CharacterSnapshot character)
    {
        var bracket = LegacyClassPkWarState.GetBracket(character);
        var process = ReadAttributeString(
                character.Attributes,
                GetLegacyClassPkWarAttributeNames(bracket, "Process", "Proces"))
            .Trim();
        var finishTimer = ReadAttributeString(
                character.Attributes,
                GetLegacyClassPkWarAttributeNames(bracket, "FinishTimerUtc", "FinishTimer", "FinishTimeUtc", "FinishTime"))
            .Trim();
        var dynamicId = ReadAttributeUInt32(
            character.Attributes,
            GetLegacyClassPkWarAttributeNames(bracket, "DynamicID", "DynamicId", "DinamicID", "DinamicId"));

        var bracketIsValid =
            bracket.TournamentType != LegacyClassPkWarTournamentType.Count &&
            bracket.TournamentLevel != LegacyClassPkWarTournamentLevel.Count;
        var parsedFinishTimer = default(DateTimeOffset);
        var known = process.Length != 0 &&
            TryParseLegacyClassPkWarFinishTimer(finishTimer, out parsedFinishTimer);

        return new LegacyClassPkWarRuntimeState(
            Known: known,
            BracketIsValid: bracketIsValid,
            TournamentType: bracket.TournamentType,
            TournamentLevel: bracket.TournamentLevel,
            Process: process,
            FinishTimer: parsedFinishTimer,
            DynamicId: dynamicId);
    }

    private static bool TryParseLegacyClassPkWarFinishTimer(string raw, out DateTimeOffset finishTimer)
    {
        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out finishTimer);
    }

    private static string[] GetLegacyClassPkWarAttributeNames(
        LegacyClassPkWarBracket bracket,
        params string[] suffixes)
    {
        var names = new List<string>();
        foreach (var prefix in GetLegacyClassPkWarAttributePrefixes(bracket))
        {
            foreach (var suffix in suffixes)
            {
                names.Add(prefix + suffix);
            }
        }

        return names.ToArray();
    }

    private static IReadOnlyList<string> GetLegacyClassPkWarAttributePrefixes(LegacyClassPkWarBracket bracket)
    {
        var typeName = bracket.TournamentType.ToString();
        var levelName = bracket.TournamentLevel.ToString();
        return
        [
            "LegacyClassPkWar",
            "ClassPkWar",
            "RuntimeClassPkWar",
            "LegacyClassPKWar",
            $"LegacyClassPkWar{typeName}{levelName}",
            $"ClassPkWar{typeName}{levelName}",
            $"RuntimeClassPkWar{typeName}{levelName}",
        ];
    }

    private readonly record struct LegacyClassPkWarRuntimeState(
        bool Known,
        bool BracketIsValid,
        LegacyClassPkWarTournamentType TournamentType,
        LegacyClassPkWarTournamentLevel TournamentLevel,
        string Process,
        DateTimeOffset FinishTimer,
        uint DynamicId);
}
