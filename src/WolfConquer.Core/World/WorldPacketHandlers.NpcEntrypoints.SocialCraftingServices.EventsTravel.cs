namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesCloudBeastNpcId = 1_503;
    private const uint LegacySocialCraftingServicesGeneralOfDefenseNpcId = 23_091;
    private const uint LegacySocialCraftingServicesAutumnNpcId = 19_317;
    private const uint LegacySocialCraftingServicesArtisanZhuRongNpcId = 22_559;
    private const uint LegacySocialCraftingServicesNobilityWarNpcId = 12_142;
    private const uint LegacySocialCraftingServicesHuntingAreaNpcId = 50_060;

    private const string LegacySocialCraftingServicesCloudBeastSource = "NpcHandler.CloudBeast";
    private const string LegacySocialCraftingServicesGeneralOfDefenseSource = "NpcHandler.GeneralOfDefense";
    private const string LegacySocialCraftingServicesAutumnSource = "NpcHandler.Autumn";
    private const string LegacySocialCraftingServicesArtisanZhuRongSource = "NpcHandler.ArtisanZhuRong";
    private const string LegacySocialCraftingServicesTcTeleporterSource = "NpcHandler.TCTeleporter";
    private const string LegacySocialCraftingServicesNobilityWarSource = "NpcHandler.NobilityWar";
    private const string LegacySocialCraftingServicesHuntingAreaSource = "NpcHandler.HuntingArea";

    private const string LegacySocialCraftingServicesCloudBeastLines = "56991-56997";
    private const string LegacySocialCraftingServicesGeneralOfDefenseLines = "57000-57030";
    private const string LegacySocialCraftingServicesAutumnLines = "57033-57072";
    private const string LegacySocialCraftingServicesArtisanZhuRongLines = "57075-57112";
    private const string LegacySocialCraftingServicesTcTeleporterLines = "57114-57167";
    private const string LegacySocialCraftingServicesNobilityWarLines = "57359-57483";
    private const string LegacySocialCraftingServicesHuntingAreaLines = "57486-57552";

    private static readonly uint[] LegacySocialCraftingServicesTcTeleporterNpcIds =
    [
        50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60,
        61, 62, 63, 64, 65, 66, 67, 68, 69, 70,
        71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
        81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
        91, 92, 93, 94, 95, 96, 97, 98, 99, 100,
        101, 102, 103, 104, 105, 106, 107, 108, 109,
        110, 111, 113, 114, 115, 116, 117, 118, 119,
        120, 121, 122, 150, 151,
    ];

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesCloudBeastAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacySocialCraftingServicesCloudBeastSource,
                ["You touch the Cloud Beast and find the giant creature quiet and gentle."],
                [new("Okay.", LegacyNpcClosedOption)],
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesGeneralOfDefenseAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        await SendLegacyNpcDialogAsync(
                context,
                LegacySocialCraftingServicesGeneralOfDefenseSource,
                [
                    "The battle against beasts in the Realm is fierce. The Infernal Phoenix appears at 22:10 near (386,386).",
                    "The four evil guards can appear at any time in random places. Rewards depend on the legacy event chest state.",
                ],
                [new("I will join.", LegacyNpcClosedOption)],
                null,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesAutumnAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesAutumnSource,
                        ["Heroes whose equipped gear is at least Super +8 with 2 sockets can register for the Perfection Ranking. You are not qualified yet."],
                        [new("Perfection Ranking?", 1), new("I will talk to you later.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesAutumnSource,
                        ["Perfection Ranking has 11 gear sections with real-time updates. Top-ranked gear is displayed with its owner's name."],
                        [new("See you.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesArtisanZhuRongAsync(
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
                        LegacySocialCraftingServicesArtisanZhuRongSource,
                        ["I can update tempered gear that still displays your old character name."],
                        [new("Update the owner's name.", 1)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesArtisanZhuRongNpcId,
                        optionId,
                        LegacySocialCraftingServicesArtisanZhuRongSource,
                        LegacySocialCraftingServicesArtisanZhuRongLines,
                        "tempered gear owner-name repair mutates equipped item owner fields and sends item updates",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesTcTeleporterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySocialCraftingServicesTcTeleporterSource,
                    ["Which area in Twin City would you like to go? I can help for free."],
                    [
                        new("Take me to the Game Manager.", 1),
                        new("Market.", 2),
                        new("Quest area.", 3),
                        new("East Gate of Twin City.", 4),
                        new("South Gate of Twin City.", 5),
                        new("Kingdom War area.", 6),
                        new("Twin City Conductress.", 7),
                    ],
                    null,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacySocialCraftingServicesTryGetTcTeleporterDestination(optionId, out var destination))
        {
            await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, destination, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesNobilityWarAsync(
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
                        LegacySocialCraftingServicesNobilityWarSource,
                        ["I am the Nobility Pole War leader. The legacy event runs from 08:00 to 08:30."],
                        [
                            new("Enter.", 1),
                            new("Claim Nobility Pole War reward.", 2),
                            new("Thanks.", LegacyNpcClosedOption),
                        ],
                        63,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await TryClaimLegacyNobilityPoleRewardAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesNobilityWarSource,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesNobilityWarNpcId,
                        optionId,
                        LegacySocialCraftingServicesNobilityWarSource,
                        LegacySocialCraftingServicesNobilityWarLines,
                        "Nobility War entry and reward claims depend on live event state, nobility rank, round ownership, CP rewards, class experience, inventory grants, and broadcasts",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesHuntingAreaAsync(
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
                        LegacySocialCraftingServicesHuntingAreaSource,
                        ["I can send you to hunting maps where monsters can drop Conquer Points."],
                        [
                            new("Red Devils [1000 CPs].", 1),
                            new("Pheasants [2000 CPs].", 2),
                            new("I see.", LegacyNpcClosedOption),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case 2:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesHuntingAreaNpcId,
                        optionId,
                        LegacySocialCraftingServicesHuntingAreaSource,
                        LegacySocialCraftingServicesHuntingAreaLines,
                        "hunting area entry charges CPs and teleports to paid farming maps, so this slice leaves balances and location unchanged",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static bool LegacySocialCraftingServicesIsTcTeleporterNpc(uint npcId)
    {
        return Array.IndexOf(LegacySocialCraftingServicesTcTeleporterNpcIds, npcId) >= 0;
    }

    private static bool LegacySocialCraftingServicesTryGetTcTeleporterDestination(
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = optionId switch
        {
            1 => new LegacyNpcTeleportDestination(1_002, 343, 434, LegacySocialCraftingServicesTcTeleporterSource),
            2 => new LegacyNpcTeleportDestination(1_002, 338, 387, LegacySocialCraftingServicesTcTeleporterSource),
            3 => new LegacyNpcTeleportDestination(1_002, 279, 473, LegacySocialCraftingServicesTcTeleporterSource),
            4 => new LegacyNpcTeleportDestination(1_002, 532, 320, LegacySocialCraftingServicesTcTeleporterSource),
            5 => new LegacyNpcTeleportDestination(1_002, 439, 471, LegacySocialCraftingServicesTcTeleporterSource),
            6 => new LegacyNpcTeleportDestination(1_002, 365, 324, LegacySocialCraftingServicesTcTeleporterSource),
            7 => new LegacyNpcTeleportDestination(1_002, 427, 357, LegacySocialCraftingServicesTcTeleporterSource),
            _ => default,
        };
        return optionId is >= 1 and <= 7;
    }
}
