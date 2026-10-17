namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCupCasinoServicesCSElitePkManagerNpcId = 19_424;
    private const uint LegacyCupCasinoServicesBronzePhoenixCupNpcId = 19_504;
    private const uint LegacyCupCasinoServicesSilverPheoenixCupNpcId = 19_505;
    private const uint LegacyCupCasinoServicesGoldenPhoenixCupNpcId = 19_506;
    private const uint LegacyCupCasinoServicesHolyPhoenixCupNpcId = 19_507;

    private const ushort LegacyCupCasinoServicesCSElitePkAvatarId = 4;

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesCSElitePkRootOptions =
    [
        new("Sign me up!", 1),
        new("Learn about the rules.", 2),
        new("Swap points for rewards.", 10),
        new("Claim participation reward.", 30),
        new("View the ranking.", 20),
        new("I will talk to you later.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesCSElitePkRuleOptions =
    [
        new("Prizes for the level 130+ division.", 3),
        new("Prizes for the level 120-129 division.", 4),
        new("Prizes for the level 100-119 division.", 5),
        new("I will talk to you later.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandleCSElitePkManagerAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.CSElitePkManager";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "The Cross-server Elite PK Tournament will begin soon. Elite fighters compete for championship glory, exclusive mount armor, and titles.",
                            "Admittance: 19:55 - 19:59 on the event date.",
                            "Kick-off time: 20:00 on the event date. Requirement: Level 100.",
                        ],
                        LegacyCupCasinoServicesCSElitePkRootOptions,
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "23167-23177",
                        npcId,
                        optionId,
                        "Cross-server Elite PK sign-up is unavailable here until inter-server tournament entry is modeled safely.",
                        "connect to the inter-server PipeClient when CrossElitePKTournament is open",
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Contestants fight in three level divisions. Higher divisions receive better prizes, and prizes are granted by mail.",
                            "The server Elite PK Top 8 qualify for the cross-server tournament. Other players register here for an accreditation card.",
                            "Leaving a match early means you cannot receive a reward.",
                        ],
                        LegacyCupCasinoServicesCSElitePkRuleOptions,
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Level 130+ prizes:",
                            "Champion: 7-day 1% Blessed Astral Phoenix, 30-day champion title and halo, 3 Astral Phoenix Fragments, and 5,000 Elite PK Point Coupons.",
                            "Second place: 7-day 1% Blessed Astral Phoenix, 30-day second-place title and halo, 2 Astral Phoenix Fragments, and 4,000 Elite PK Point Coupons.",
                            "Third place: 7-day 1% Blessed Astral Phoenix, 30-day third-place title and halo, 1 Astral Phoenix Fragment, and 3,500 Elite PK Point Coupons.",
                            "Top 8: 30-day Top 8 title and halo, plus 3,000 Elite PK Point Coupons. Other finishers may claim 500 coupons.",
                        ],
                        LegacyCupCasinoServicesCloseOptions,
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Level 120-129 prizes:",
                            "Champion: 30-day champion halo and 3,000 Elite PK Point Coupons.",
                            "Second place: 30-day second-place halo and 2,500 Elite PK Point Coupons.",
                            "Third place: 30-day third-place halo and 2,000 Elite PK Point Coupons.",
                            "Top 8: 30-day Top 8 halo and 1,500 Elite PK Point Coupons. Other finishers may claim 500 coupons.",
                        ],
                        LegacyCupCasinoServicesCloseOptions,
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Level 100-119 prizes:",
                            "Champion: 2,000 Elite PK Point Coupons.",
                            "Second place: 1,500 Elite PK Point Coupons.",
                            "Third place: 1,200 Elite PK Point Coupons.",
                            "Top 8: 1,000 Elite PK Point Coupons. Other finishers may claim 500 coupons.",
                        ],
                        LegacyCupCasinoServicesCloseOptions,
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "23124-23129",
                        npcId,
                        optionId,
                        "Elite PK point reward exchange is unavailable here until the exchange-shop inventory gate is modeled safely.",
                        "open exchange shop 19424 when item 3300035 is present",
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "23105-23121",
                        npcId,
                        optionId,
                        "Cross-server Elite PK rankings are unavailable here until the global ranking table is modeled safely.",
                        "read CrossElitePKTournament.RanksElite and send Elite PK ranking packets",
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 30:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        "23083-23103",
                        npcId,
                        optionId,
                        "Cross-server Elite PK participation rewards are unavailable here until reward payout is modeled safely.",
                        "claim CrossElitePKTournament reward coupons and grant inventory items",
                        LegacyCupCasinoServicesCSElitePkAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandlePhoenixCupAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        string source,
        IReadOnlyList<string> texts,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        if (optionId != 0)
        {
            return false;
        }

        await SendLegacyNpcDialogAsync(
                context,
                source,
                texts,
                LegacyCupCasinoServicesCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static readonly string[] LegacyCupCasinoServicesHolyPhoenixCupTexts =
    [
        "*Holy Phoenix Cup*",
        "Basic attributes: HP +1200, MP +1200, Defense +1000, P-Strike +1%, M-Strike +1%, Break +1%, Anti-break +1%.",
        "Tempered bonuses: Lv.3 Toxin Eraser, Lv.5 Draining Touch, Lv.2 Blood Spawn, Lv.5 Free Soul, Lv.5 Killing Flash, Lv.3 Straight Life, HP +500.",
        "Duration: 90 days.",
    ];

    private static readonly string[] LegacyCupCasinoServicesGoldenPhoenixCupTexts =
    [
        "*Golden Phoenix Cup*",
        "Basic attributes: HP +1000, MP +1000, Defense +500, 1% Blessed.",
        "Tempered bonuses: Lv.3 Toxin Eraser, Lv.4 Draining Touch, Lv.2 Blood Spawn, Lv.4 Free Soul, Lv.4 Killing Flash, Lv.3 Straight Life, HP +300.",
        "Duration: 90 days.",
    ];

    private static readonly string[] LegacyCupCasinoServicesSilverPhoenixCupTexts =
    [
        "*Silver Phoenix Cup*",
        "Basic attributes: HP +900, MP +900, Defense +300, 1% Blessed.",
        "Tempered bonuses: Lv.3 Toxin Eraser, Lv.3 Draining Touch, Lv.1 Blood Spawn, Lv.3 Free Soul, Lv.3 Killing Flash, Lv.2 Straight Life.",
        "Duration: 60 days.",
    ];

    private static readonly string[] LegacyCupCasinoServicesBronzePhoenixCupTexts =
    [
        "*Bronze Phoenix Cup*",
        "Basic attributes: HP +800, MP +800, Defense +150, 1% Blessed.",
        "Tempered bonuses: Lv.3 Toxin Eraser, Lv.2 Draining Touch, Lv.2 Killing Flash, Lv.2 Free Soul.",
        "Duration: 30 days.",
    ];
}
