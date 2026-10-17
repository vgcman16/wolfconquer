namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDisCityBattlefieldServicesTreasureThiefNpcId = 52_000;
    private const uint LegacyDisCityBattlefieldServicesTreasureLeaveNpcId = 4_026_320;
    private const uint LegacyDisCityBattlefieldServicesFindGmNpcId = 55_551;
    private const uint LegacyDisCityBattlefieldServicesDbShowerNpcId = 666;
    private const uint LegacyDisCityBattlefieldServicesGwJailNpcId = 4_026_307;
    private const uint LegacyDisCityBattlefieldServicesStartNpcId = 4_026_306;
    private const uint LegacyDisCityBattlefieldServicesPrizeOfficerNpcId = 47;
    private const uint LegacyDisCityBattlefieldServicesTeratoTwinNpcId = 7_927;

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesSimpleSignUpOptions =
    [
        new("Sign up.", 1),
        new("I will come later.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleSimpleTournamentAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        string rootText,
        string unavailableText,
        string gapReason,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [rootText],
                        LegacyDisCityBattlefieldServicesSimpleSignUpOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        npcId,
                        optionId,
                        source,
                        unavailableText,
                        gapReason,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleTeratoTwinAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.TeratoTwin", "NpcHandler.cs:29695-29734");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Sorry, this event has ended."],
                        [new LegacyNpcDialogOption("Hard luck.", LegacyNpcClosedOption)],
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesTeratoTwinNpcId,
                        optionId,
                        source,
                        "Terato Twin monster spawning is not available from this runtime yet.",
                        "Source checks five Meteors, one Dragon Pill, map mob id 20060, consumes items, and spawns event state.",
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandlePrizeOfficerAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.PrizeOfficer", "NpcHandler.cs:29790-29812");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Please wait until the system checks the donation."],
                        [new LegacyNpcDialogOption("Ok, I'm waiting.", 1)],
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesPrizeOfficerNpcId,
                        optionId,
                        source,
                        "Prize Officer donation checks are not available from this runtime yet.",
                        "Source depends on legacy inventory-space and donation verification state.",
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleStartNpcAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.StartNpc", "NpcHandler.cs:30299-30532");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["This is the way to Twin City. Many people are gathering there."],
                        [
                            new LegacyNpcDialogOption("Teleport me to Twin City.", 3),
                            new LegacyNpcDialogOption("Let me think.", LegacyNpcClosedOption),
                        ],
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await LegacyDisCityBattlefieldServicesSendUnavailableAsync(
                        context,
                        LegacyDisCityBattlefieldServicesStartNpcId,
                        optionId,
                        source,
                        "Starter rewards are not available from this runtime yet.",
                        "Source grants class spells, 2,000,000 silver, multiple starter items, and then teleports to Twin City.",
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleTreasureLeaveAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.TreasureLeave", "NpcHandler.cs:30533-30557");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Hello there. Let me know if you want to go to Twin City."],
                        LegacyDisCityBattlefieldServicesTwinCityLeaveOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Are you sure you want to leave for Twin City?"],
                        LegacyDisCityBattlefieldServicesTwinCityConfirmOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyDisCityBattlefieldServicesTreasureLeaveNpcId,
                        optionId,
                        LegacyDisCityBattlefieldServicesTwinCityDestination(source.SourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                return true;
            default:
                return false;
        }
    }
}
