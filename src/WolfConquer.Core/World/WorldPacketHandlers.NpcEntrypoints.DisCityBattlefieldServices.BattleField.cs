using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDisCityBattlefieldServicesBattleFieldMainNpcId = 10_123;
    private const uint LegacyDisCityBattlefieldServicesBattleFieldMap1NpcId = 4_026_308;
    private const uint LegacyDisCityBattlefieldServicesBattleFieldMap2NpcId = 4_026_309;
    private const uint LegacyDisCityBattlefieldServicesBattleFieldMap3NpcId = 4_026_310;

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesBattleFieldRootOptions =
    [
        new("Top Hero.", 1),
        new("Sign up.", 2),
        new("About this.", 3),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesBattleFieldMap1Options =
    [
        new("Send me there.", 1),
        new("Send me in Twin City.", 2),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesBattleFieldMap3Options =
    [
        new("Send me in Twin City.", 2),
        new("Let me think.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleBattleFieldMainAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.BattleFieldMain", "NpcHandler.cs:29469-29564");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["The BattleField takes place every day. Come prove yourself when the event is open."],
                        LegacyDisCityBattlefieldServicesBattleFieldRootOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesBattleFieldMainNpcId,
                        optionId,
                        source,
                        "BattleField top hero rankings are not available from this runtime yet.",
                        "Source reads MsgSchedules.Tournaments[BattleField].Rank3 and renders live participant scores.",
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesBattleFieldMainNpcId,
                        optionId,
                        source,
                        "BattleField sign-up is not available from this runtime yet.",
                        "Source depends on MsgSchedules.CurrentTournament.Type == BattleField and CurrentTournament.Join.",
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["BattleField has 3 maps. Monsters grant 10, 20, and 30 points on the first, second, and third maps. Points convert to CPs when you leave or the event ends."],
                        [new LegacyNpcDialogOption("Thank you.", LegacyNpcClosedOption)],
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleBattleFieldMapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        string nextMapText,
        uint advancePointCost,
        LegacyNpcTeleportDestination advanceDestination,
        string insufficientPointsText,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [$"You have {LegacyDisCityBattlefieldServicesBattleFieldPoints(session).ToString(CultureInfo.InvariantCulture)} BattleFieldPoints. {nextMapText}"],
                        LegacyDisCityBattlefieldServicesBattleFieldMap1Options,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyDisCityBattlefieldServicesTryApplyBattleFieldAdvanceAsync(
                        context,
                        npcId,
                        source,
                        optionId,
                        advancePointCost,
                        advanceDestination,
                        insufficientPointsText,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await LegacyDisCityBattlefieldServicesTryApplyBattleFieldPointPayoutAsync(
                        context,
                        npcId,
                        source,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleBattleFieldMap3Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.BattleFieldMap3", "NpcHandler.cs:29663-29694");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [$"You have {LegacyDisCityBattlefieldServicesBattleFieldPoints(session).ToString(CultureInfo.InvariantCulture)} BattleFieldPoints."],
                        LegacyDisCityBattlefieldServicesBattleFieldMap3Options,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await LegacyDisCityBattlefieldServicesTryApplyBattleFieldPointPayoutAsync(
                        context,
                        LegacyDisCityBattlefieldServicesBattleFieldMap3NpcId,
                        source,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static ulong LegacyDisCityBattlefieldServicesBattleFieldPoints(WorldSession session)
    {
        return ReadAttributeUInt64(session.Character.Attributes, "BattleFieldPoints");
    }
}
