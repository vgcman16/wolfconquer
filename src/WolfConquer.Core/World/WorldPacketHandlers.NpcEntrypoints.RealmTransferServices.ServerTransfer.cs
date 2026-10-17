using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRealmTransferServicesServerTransferNpcId = 15_702;
    private const uint LegacyRealmTransferServicesMrRayzoEuNpcId = 52_100;
    private const uint LegacyRealmTransferServicesMrRayzoUsNpcId = 52_101;
    private const uint LegacyRealmTransferServicesRealmEnvoyNpcId = 18_787;

    private const ushort LegacyRealmTransferServicesTransferAvatarId = 186;
    private const ushort LegacyRealmTransferServicesKeeperAvatarId = 7;
    private const ushort LegacyRealmTransferServicesRealmEnvoyAvatarId = 43;

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesServerTransferRootOptions =
    [
        new("Yeah,~tell~me~more~about~it.", 1),
        new("I~want~to~transfer~now.", 10),
        new("Nothing.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesRealmReturnOptions =
    [
        new("Return~to~the~Realm.", 1),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesServerTransferAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.ServerTransfer";
        const string sourceLines = "21419-21470";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "I am responsible for server transfers.",
                            "I can help anytime except 07:00-09:30 and 15:00-17:30.",
                        ],
                        LegacyRealmTransferServicesServerTransferRootOptions,
                        LegacyRealmTransferServicesTransferAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Before transferring, cancel all relationships on this server, including guild, clan, mentor, trade partner, and marriage.",
                            "Make sure no detained equipment or house item boxes still need your attention.",
                            "Claim all TQ Point Cards, unread messages, and mentor experience.",
                            "After that, the transfer costs 200,000 CPs.",
                        ],
                        LegacyRealmTransferServicesCloseOptions,
                        LegacyRealmTransferServicesTransferAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesServerTransferNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "list available transfer servers from GroupServerList.CanTransferServers",
                        "Server transfer selection is unavailable here until the inter-server transfer list is modeled safely.",
                        LegacyRealmTransferServicesTransferAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }

        if (optionId > 100)
        {
            await SendLegacyRealmTransferServicesGapAsync(
                    context,
                    LegacyRealmTransferServicesServerTransferNpcId,
                    optionId,
                    source,
                    sourceLines,
                    "validate relationships, secondary password, and initialize server transfer",
                    "Server transfers are unavailable here until inter-server transfer initialization is modeled safely.",
                    LegacyRealmTransferServicesTransferAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesKeeperAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        string source,
        string sourceLines,
        string serverName,
        uint serverId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            $"I am the keeper of the [{serverName}] server. You will find a keeper like me in each server.",
                            "To visit another server, register with the destination keeper in the Realm.",
                            "I can also help you return to your original server. Please learn the rules before taking the trip.",
                        ],
                        [
                            new LegacyNpcDialogOption($"Take~me~to~server~[{serverName}].", 1),
                            new LegacyNpcDialogOption("How~to~travel~across~server?", 10),
                            new LegacyNpcDialogOption("What~should~I~pay~attention~to?", 11),
                        ],
                        LegacyRealmTransferServicesKeeperAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        npcId,
                        optionId,
                        source,
                        sourceLines,
                        $"connect to cross-server keeper destination server id {serverId.ToString(CultureInfo.InvariantCulture)}",
                        "Cross-server keeper registration is unavailable here until inter-server connection handoff is modeled safely.",
                        LegacyRealmTransferServicesKeeperAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "To go to another server, enter the Realm from your server and register with the destination keeper there.",
                            "To return, transit through the Realm again and find the keeper for your original server.",
                            "Please learn every rule and warning before taking a cross-server trip.",
                        ],
                        [new LegacyNpcDialogOption("What~should~I~pay~attention~to?", 11)],
                        LegacyRealmTransferServicesKeeperAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "When visiting a new server, your level, rebirths, equipment, nobility, mentor data, guild battle power, and clan battle power are registered there.",
                            "Only chat and PK functions are available. Talk, Whisper, Team, Guild, Clan, Friend, Service, World, and Ally channels are allowed.",
                            "If you are killed in the Realm, you will revive there.",
                        ],
                        [new LegacyNpcDialogOption("What~functions~are~unavailable?", 12)],
                        LegacyRealmTransferServicesKeeperAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "In the Realm, money, item trade, forging, social contact, Arena, mentor gains, welfare, enlightenment, flowers, and PvP matches are unavailable.",
                            "Those functions return after I send you back to your original server.",
                        ],
                        LegacyRealmTransferServicesCloseOptions,
                        LegacyRealmTransferServicesKeeperAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesRealmEnvoyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.RealmEnvoy";
        const string sourceLines = "24354-24378";

        switch (optionId)
        {
            case 0:
                if (LegacyRealmTransferServicesIsOnOwnServer(session))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["I serve the Realm. After you travel to other servers, find me to return to the Realm."],
                            LegacyRealmTransferServicesCloseOptions,
                            LegacyRealmTransferServicesRealmEnvoyAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["I have the code to enter the Realm. If you want to return to the Realm, I can help you."],
                            LegacyRealmTransferServicesRealmReturnOptions,
                            LegacyRealmTransferServicesRealmEnvoyAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return true;

            case 1:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesRealmEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "send ConnectionInfoCreate to GroupServerList.InterServer.ID",
                        "Realm return travel is unavailable here until inter-server connection handoff is modeled safely.",
                        LegacyRealmTransferServicesRealmEnvoyAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
