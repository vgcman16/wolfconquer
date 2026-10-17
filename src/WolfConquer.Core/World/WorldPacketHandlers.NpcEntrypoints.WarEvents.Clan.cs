using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClanDominationNpcId = 15_554;
    private const uint LegacyClanWarScoreNpcId = 6_526;
    private const ushort LegacyClanDominationAvatarId = 110;
    private const ushort LegacyClanWarScoreAvatarId = 63;
    private const string LegacyClanDominationSourceMethod = "NpcHandler.ClanDomination";
    private const string LegacyClanWarScoreSourceMethod = "NpcHandler.ClanWarScore";
    private const string LegacyClanWarScoreProcessAttribute = "LegacyClanWarScoreProcess";
    private const string ClanWarEventRewardGapReason =
        "legacy reward branch depends on MsgSchedules clan tournament winners, leader reward counts, claimed-player sets, inventory grants, study points, inner power, clan donation, and large CP grants not represented by current runtime state.";

    private static readonly LegacyNpcDialogOption[] LegacyClanDominationOptions =
    [
        new("Claim the Twin City flag reward.", 1),
        new("Claim the Phoenix Castle flag reward.", 2),
        new("Claim the Ape City flag reward.", 3),
        new("Claim the Desert City flag reward.", 4),
        new("Claim the Bird Island flag reward.", 5),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClanWarScoreOptions =
    [
        new("Enter", 1),
        new("Claim reward", 2),
        new("Just looking around.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClanWarEventClosedOptions =
    [
        new("Okay.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyClanWarEventNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyClanDominationNpcId => await TryHandleLegacyClanDominationAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyClanWarScoreNpcId => await TryHandleLegacyClanWarScoreAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyClanDominationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyClanDominationSourceMethod,
                    [
                        "This is the Clan Domination reward center.",
                        "Only the winning clan leader can claim a flag reward. The war starts at 22:00.",
                        "There are five flag poles, and one starts every 10 minutes.",
                    ],
                    LegacyClanDominationOptions,
                    LegacyClanDominationAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyClanDominationFlagName(optionId, out var flagName))
        {
            await SendLegacyClanWarRewardUnavailableAsync(
                    context,
                    session,
                    LegacyClanDominationNpcId,
                    optionId,
                    LegacyClanDominationSourceMethod,
                    $"Clan Domination {flagName} reward",
                    "This flag reward cannot be claimed yet. The winning clan and claimed-leader state are not loaded on this server.",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyClanWarScoreAsync(
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
                        LegacyClanWarScoreSourceMethod,
                        ["Hi, I manage Clan War Score. What can I do for you? The event runs from 16:30 to 16:59."],
                        LegacyClanWarScoreOptions,
                        LegacyClanWarScoreAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                if (IsLegacyClanWarScoreAlive(session.Character))
                {
                    await SendLegacyNpcTeleportAsync(
                            context,
                            session,
                            LegacyClanWarScoreNpcId,
                            optionId,
                            new LegacyNpcTeleportDestination(3_839, 100, 100, LegacyClanWarScoreSourceMethod),
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyClanWarScoreSourceMethod,
                        ["The Clan War Score event has not started yet."],
                        LegacyClanWarEventClosedOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                await LogLegacyClanWarEventGapAsync(
                        context,
                        session,
                        LegacyClanWarScoreNpcId,
                        optionId,
                        LegacyClanWarScoreSourceMethod,
                        "MsgSchedules.ClanWarScore.Proces is not globally loaded; set LegacyClanWarScoreProcess=Alive on the character to model the source entry teleport.")
                    .ConfigureAwait(false);
                return true;
            case 2:
                await TryClaimLegacyClanWarScoreRewardAsync(
                        context,
                        session,
                        optionId,
                        LegacyClanWarScoreSourceMethod,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyClanWarRewardUnavailableAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string rewardName,
        string dialogText,
        CancellationToken cancellationToken)
    {
        await LogLegacyClanWarEventGapAsync(
                context,
                session,
                npcId,
                optionId,
                sourceMethod,
                $"{rewardName}: {ClanWarEventRewardGapReason}")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [dialogText],
                LegacyClanWarEventClosedOptions,
                sourceMethod == LegacyClanDominationSourceMethod ? LegacyClanDominationAvatarId : null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LogLegacyClanWarEventGapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string reason)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    npcId,
                    opcode: WorldPacketOpcodes.LegacyNpcReply,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: optionId,
                    interactType: 1,
                    source: sourceMethod,
                    reason: reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc ClanWarEvent " +
                $"{FormatSession(context)} npcId={npcId.ToString(CultureInfo.InvariantCulture)} " +
                $"option={optionId.ToString(CultureInfo.InvariantCulture)} source={sourceMethod}; {reason}")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyClanWarScoreAlive(CharacterSnapshot character)
    {
        var process = ReadAttributeString(
            character.Attributes,
            LegacyClanWarScoreProcessAttribute,
            "ClanWarScoreProcess",
            "RuntimeClanWarScoreProcess");
        return process.Equals("Alive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetLegacyClanDominationFlagName(byte optionId, out string flagName)
    {
        flagName = optionId switch
        {
            1 => "Twin City",
            2 => "Phoenix Castle",
            3 => "Ape City",
            4 => "Desert City",
            5 => "Bird Island",
            _ => string.Empty,
        };
        return flagName.Length > 0;
    }
}
