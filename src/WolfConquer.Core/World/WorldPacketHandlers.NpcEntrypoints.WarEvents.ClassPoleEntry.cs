using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly string[] LegacyClassPoleWarEntrySchedules =
    [
        "MsgArcherMaster",
        "MsgFireMaster",
        "MsgKungfuKing",
        "MsgMonkMaster",
        "MsgNinjaMaster",
        "MsgPirateMaster",
        "MsgTaoistMaster",
        "MsgTrojanMaster",
        "MsgWarriorGod",
        "MsgWindWalker",
        "MsgThnderStrike",
    ];

    private static async ValueTask TryEnterLegacyClassPoleWarAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacyClassPoleWarEntryState(session.Character);
        if (state.Known && !state.AllAlive)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("Class PK has not started yet. Please wait.").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc ClassPoleWar enter " +
                    $"{FormatSession(context)} source={sourceMethod} option={optionId}; " +
                    $"schedule={state.BlockingSchedule} process={state.BlockingProcess} is not Alive.")
                .ConfigureAwait(false);
            return;
        }

        var enteredSession = ReviveLegacyPoleWarEventSessionIfNeeded(session);
        await SendLegacyNpcTeleportAsync(
                context,
                enteredSession,
                LegacyClassPoleWarNpcId,
                optionId,
                new LegacyNpcTeleportDestination(10_005, 350, 306, sourceMethod),
                cancellationToken)
            .ConfigureAwait(false);

        if (state.Known)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc ClassPoleWar enter " +
                    $"{FormatSession(context with { Session = enteredSession })} source={sourceMethod} option={optionId}; " +
                    "all modeled class tournament processes are Alive.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ClassPoleWar enter state unavailable " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; " +
                "legacy requires all class tournament processes to be Alive before teleporting, " +
                "but local global tournament state is not represented, so the source destination fallback was used.")
            .ConfigureAwait(false);
    }

    private static LegacyClassPoleWarEntryState ReadLegacyClassPoleWarEntryState(CharacterSnapshot character)
    {
        var sawAnyProcess = false;
        foreach (var scheduleName in LegacyClassPoleWarEntrySchedules)
        {
            var process = ReadAttributeString(
                    character.Attributes,
                    GetLegacyClassPoleWarAttributeNames(scheduleName, "Process", "Proces"))
                .Trim();
            if (process.Length == 0)
            {
                return sawAnyProcess
                    ? new LegacyClassPoleWarEntryState(true, false, scheduleName, "missing")
                    : default;
            }

            sawAnyProcess = true;
            if (!IsLegacyClassPoleWarEntryAliveProcess(process))
            {
                return new LegacyClassPoleWarEntryState(true, false, scheduleName, process);
            }
        }

        return new LegacyClassPoleWarEntryState(true, true, string.Empty, string.Empty);
    }

    private static bool IsLegacyClassPoleWarEntryAliveProcess(string process)
    {
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase) ||
            process.Equals("ProcesType.Alive", StringComparison.OrdinalIgnoreCase) ||
            process == "1";
    }

    private readonly record struct LegacyClassPoleWarEntryState(
        bool Known,
        bool AllAlive,
        string BlockingSchedule,
        string BlockingProcess);
}
