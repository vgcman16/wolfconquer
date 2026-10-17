using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRealmTransferServicesKingdomMissionEnvoyNpcId = 17_400;

    private const ushort LegacyRealmTransferServicesKingdomAvatarId = 6;

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomForeignServerOptions =
    [
        new("Return~to~the~Realm.", 2),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomRootOptions =
    [
        new("Enter~the~Realm.", 1),
        new("Kingdom~Mission.", 10),
        new("Submit~Gold~Bricks.~(special)", 20),
        new("Swap~deeds~for~rewards.", 30),
        new("How~to~travel~across~server?", 40),
        new("What~should~I~pay~attention~to?", 50),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomRewardOptions =
    [
        new("Outstanding~Exploit~Pack.~(50)", 31),
        new("Brilliant~Exploit~Pack.~(150)", 32),
        new("Supreme~Exploit~Pack.~(300)", 33),
        new("Buy~Kingdom~Deeds.", 34),
        new("What~is~in~the~packs?", 37),
        new("I~will~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomMissionOptions =
    [
        new("[Realm]~Beasts~of~Legend.", 90),
        new("[Realm]~Thunder~Strike.", 60),
        new("[Realm]~Ashes~of~Anger.", 11),
        new("[Realm]~Scramble~for~Justice.", 80),
        new("[Realm]~Crystal~Bounty.", 70),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomTravelOptions =
    [
        new("What~should~I~pay~attention~to?", 41),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomTravelWarningOptions =
    [
        new("What~functions~will~be~unavailable?", 42),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesKingdomAttentionOptions =
    [
        new("What~functions~will~be~unavailable?", 51),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesKingdomMissionEnvoyAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.KingdomMissionEnvoy";
        const string sourceLines = "24381-24829";

        switch (optionId)
        {
            case 0:
                await SendLegacyRealmTransferServicesKingdomRootAsync(context, session, source, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case 2:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesKingdomMissionEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "connect the player to GroupServerList.InterServer",
                        "Kingdom Realm travel is unavailable here until inter-server connection handoff is modeled safely.",
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesKingdomMissionEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "open union gold brick submit dialog",
                        "Kingdom gold brick submission is unavailable here until union brick state is modeled safely.",
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 30:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "You can collect up to 300 Kingdom Deeds in a day from Kingdom Missions.",
                            "If you have 50, 150, or 300 deeds, you can swap them for corresponding rewards.",
                            $"Your Kingdom Deeds: {LegacyRealmTransferServicesKingdomDeeds(session).ToString(CultureInfo.InvariantCulture)}.",
                        ],
                        LegacyRealmTransferServicesKingdomRewardOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 31:
            case 32:
            case 33:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesKingdomMissionEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "spend Kingdom Deeds and grant an exploit pack",
                        "Kingdom Deed reward packs are unavailable here until deed persistence and item grants are modeled safely.",
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 34:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            $"You have {LegacyRealmTransferServicesKingdomDeeds(session).ToString(CultureInfo.InvariantCulture)} Kingdom Deeds.",
                            "To fill your daily total to 300 deeds, the costs are 990 CPs for 0-49 deeds, 540 CPs for 50-149 deeds, and 270 CPs for 150-299 deeds.",
                        ],
                        [
                            new LegacyNpcDialogOption("Pay~CPs~to~fill~up~with~deeds.", 35),
                            new LegacyNpcDialogOption("I~will~think~about~it.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 35:
                if (ReadAttributeUInt64(session.Character.Attributes, "BuyKingdomDeeds", "BoughtKingdomDeeds") != 0)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["You already bought deeds today. Please come back tomorrow."],
                            LegacyRealmTransferServicesCloseOptions,
                            LegacyRealmTransferServicesKingdomAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var deedCost = LegacyRealmTransferServicesKingdomDeedFillCost(session);
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [$"Are you sure you want to pay {deedCost.ToString(CultureInfo.InvariantCulture)} CPs to fill up your Kingdom Deeds?"],
                        [
                            new LegacyNpcDialogOption("Yes.", 36),
                            new LegacyNpcDialogOption("Wait,~I~have~not~decided.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 36:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesKingdomMissionEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "spend CPs, set BuyKingdomDeeds, and set KingDomDeeds to 300",
                        "Buying Kingdom Deeds is unavailable here until CP persistence and deed state are modeled safely.",
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 37:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "50 Kingdom Deeds can be swapped for an Outstanding Exploit Pack.",
                            "150 Kingdom Deeds can be swapped for a Brilliant Exploit Pack.",
                            "300 Kingdom Deeds can be swapped for a Supreme Exploit Pack.",
                            "The packs grant war exploits, potency, union fund contribution, and some higher-tier champion rewards in the legacy server.",
                        ],
                        LegacyRealmTransferServicesCloseOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "A real hero must harbor great ambition for the kingdom.",
                            "Heroes who have reached Level 110 or the first rebirth can take cross-server missions in the Realm.",
                        ],
                        LegacyRealmTransferServicesKingdomMissionOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "In Ashes of Anger, ignite the explosive device and burn the Deserted Village into ashes.",
                            "If you succeed, you can claim a valuable reward.",
                        ],
                        [
                            new LegacyNpcDialogOption("Claim~my~rewards.", 12),
                            new LegacyNpcDialogOption("Count~on~me!", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 12:
            case 61:
            case 62:
            case 91:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesKingdomMissionEnvoyNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "complete or claim a Kingdom Mission reward",
                        "Kingdom Mission reward claims are unavailable here until quest progress, CP persistence, and item grants are modeled safely.",
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 40:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "To travel to another server, find me to enter the Realm first.",
                            "Then register with the destination server keeper in the Realm.",
                            "To return, transit through the Realm and find the keeper of your original server.",
                        ],
                        LegacyRealmTransferServicesKingdomTravelOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 41:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "When visiting a new server, your level, rebirths, equipment, nobility, mentor data, guild battle power, and clan battle power are registered there.",
                            "During Plunder War, you can travel to other servers through the Realm. Only chat and PK are available.",
                            "If you are killed in the Realm, you will revive there.",
                        ],
                        LegacyRealmTransferServicesKingdomTravelWarningOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 42:
            case 51:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "In the Realm, money, item trade, forging, social contact, Arena, mentor gains, welfare, enlightenment, flowers, and PvP matches are unavailable.",
                            "Those functions return after you go back to your original server.",
                        ],
                        LegacyRealmTransferServicesCloseOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 50:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "When visiting a new server, your level, rebirths, equipment, nobility, mentor data, guild battle power, and clan battle power are registered there.",
                            "Only chat and PK are available now. Talk, Whisper, Team, Guild, Clan, Friend, Service, World, and Ally channels are allowed.",
                            "If you are killed in the Realm, you will revive there.",
                        ],
                        LegacyRealmTransferServicesKingdomAttentionOptions,
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 60:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "For Thunder Strike, disable enemy soldiers, officers, and Kylin Chariots in the Realm.",
                            "Collect 300 Strike Points to claim a bounty. You can also earn War Exploits from enemy defenses and officers.",
                        ],
                        [
                            new LegacyNpcDialogOption("Claim~my~bounty.", 61),
                            new LegacyNpcDialogOption("Directly~complete~it.~(500~CPs)", 62),
                            new LegacyNpcDialogOption("Learn~about~other~things.", 10),
                            new LegacyNpcDialogOption("I~am~not~ready~yet!", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 70:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "The Realm urgently needs helpers against manic enemies.",
                            "After killing enough enemies, you can claim a starry bounty from the Realm Crystal.",
                        ],
                        [new LegacyNpcDialogOption("Count~on~me!", LegacyNpcClosedOption)],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 80:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Bloodthirsty devils have occupied the Deserted Village in the Realm.",
                            "Break the four Dark Crystals, defeat the Alluring Witch, and rewards will be offered to contributors.",
                        ],
                        [new LegacyNpcDialogOption("Count~on~me!", LegacyNpcClosedOption)],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 90:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "The four mythological creatures have appeared together in the Realm.",
                            "Defeat three defenders, then challenge the divine beasts for their treasures.",
                        ],
                        [
                            new LegacyNpcDialogOption("Claim~my~rewards.", 91),
                            new LegacyNpcDialogOption("I~am~not~ready~yet!", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesKingdomAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyRealmTransferServicesKingdomRootAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string source,
        CancellationToken cancellationToken)
    {
        if (!LegacyRealmTransferServicesIsOnOwnServer(session))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["I have the code to enter the Realm. If you want to return to the Realm, I can help you."],
                    LegacyRealmTransferServicesKingdomForeignServerOptions,
                    LegacyRealmTransferServicesKingdomAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Level < 110 && session.Character.Reborn < 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You should reach Level 110 or the first rebirth before you can enter the Realm."],
                    LegacyRealmTransferServicesCloseOptions,
                    LegacyRealmTransferServicesKingdomAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcDialogAsync(
                context,
                source,
                [
                    "A real hero must harbor great ambition for the kingdom.",
                    "In the name of Conquer, I am calling heroes who have reached Level 110 or the first rebirth for cross-server missions.",
                    "Go to the Realm, and you will find a way to explore a new world out of this world.",
                    $"You have earned {LegacyRealmTransferServicesKingdomDeeds(session).ToString(CultureInfo.InvariantCulture)} Kingdom Deeds.",
                ],
                LegacyRealmTransferServicesKingdomRootOptions,
                LegacyRealmTransferServicesKingdomAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ulong LegacyRealmTransferServicesKingdomDeeds(WorldSession session)
    {
        return ReadAttributeUInt64(session.Character.Attributes, "KingDomDeeds", "KingdomDeeds");
    }

    private static ulong LegacyRealmTransferServicesKingdomDeedFillCost(WorldSession session)
    {
        var deeds = LegacyRealmTransferServicesKingdomDeeds(session);
        if (deeds >= 150)
        {
            return 270;
        }

        return deeds >= 50 ? 540UL : 990UL;
    }
}
