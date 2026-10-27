using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySubclassMasterPerformerNpcId = 7_776;
    private const uint LegacySubclassMasterApothecaryNpcId = 7_777;
    private const uint LegacySubclassMasterMartialArtistNpcId = 7_778;
    private const uint LegacySubclassMasterWarlockNpcId = 7_779;
    private const uint LegacySubclassMasterSageNpcId = 7_780;
    private const uint LegacySubclassMasterChiMasterNpcId = 7_781;
    private const uint LegacySubclassMasterLeadWranglerNpcId = 8_616;

    private const byte LegacySubclassMasterMartialArtistId = 1;
    private const byte LegacySubclassMasterWarlockId = 2;
    private const byte LegacySubclassMasterChiMasterId = 3;
    private const byte LegacySubclassMasterSageId = 4;
    private const byte LegacySubclassMasterApothecaryId = 5;
    private const byte LegacySubclassMasterPerformerId = 6;
    private const byte LegacySubclassMasterWranglerId = 9;

    private const ushort LegacySubclassMasterStudyCost = 800;
    private const byte LegacyPerformerPromotionFirstOptionId = 41;
    private const ushort LegacyPerformerAvatarId = 110;
    private const uint LegacyPerformerCardItemId = 711_679;
    private const uint LegacyPerformerOneOrchidItemId = 753_001;
    private const uint LegacyPerformerThreeOrchidItemId = 753_003;
    private const uint LegacyPerformerNineOrchidItemId = 753_009;
    private const int LegacyPerformerOrchidCardThreshold = 15;
    private const string LegacyPerformerOrchidCountAttribute = "GiveFlowersToPerformer";
    private const string LegacyPerformerSourceMethod = "NpcHandler.Performer";
    private const string LegacyPerformerSourceRange = "NpcHandler.cs:11387-12416";
    private const string LegacyPerformerOrchidSourceRange = "NpcHandler.cs:11950-12056";
    private const string LegacyPerformerDanceSourceRange = "NpcHandler.cs:12181-12380";
    private const string LegacyPerformerSpellAddSourceRange = "MrRayzo.Role.Instance\\Spell.cs:61-83";

    private static readonly LegacyNpcDialogOption[] LegacySubclassMasterCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySubclassMasterOhOptions =
    [
        new("Oh.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySubclassMasterAlrightOptions =
    [
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerRootOptions =
    [
        new("Yes,~I`d~like~to~join!", 20),
        new("I~want~to~get~promoted.", 40),
        new("I~want~to~learn~new~dances.", 3),
        new("How~can~I~get~promoted?", 1),
        new("Nice.~I`ll~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerJoinOptions =
    [
        new("Pay~15~Orchids~to~join.", 23),
        new("Pay~800~Study~Points~to~join.", 21),
        new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerStudyConfirmOptions =
    [
        new("Yes,~I`ve~decided!", 22),
        new("No,~I~changed~my~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerCardConfirmOptions =
    [
        new("Sure.", 25),
        new("I`ve~changed~my~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerEarnedCardConfirmOptions =
    [
        new("Sure.", 27),
        new("I`ve~changed~my~mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerOrchidOptions =
    [
        new("1~Orchid.", 26),
        new("3~Orchids.", 28),
        new("9~Orchids.", 29),
        new("How~many~have~I~given~you?", 30),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerDanceOptions =
    [
        new("Learn~Partner~Dances.", 9),
        new("The~Battle~Dance.", 8),
        new("Triumph.", 7),
        new("The~Step~Stomp.", 6),
        new("Moonlight.", 5),
        new("Snow~Wind.", 4),
        new("I`ll~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyPerformerPartnerDanceOptions =
    [
        new("Learn~Phoenix.~(P3~Performer)", 12),
        new("Melody~(Phase~6).", 11),
        new("Tipsy~(Phase~9).", 10),
        new("Previous.", 3),
    ];

    private static readonly LegacyPerformerDanceDefinition[] LegacyPerformerDances =
    [
        new(
            OptionId: 12,
            SpellId: 1_420,
            DanceName: "Phoenix",
            RequiredPhase: 3,
            PhaseGateText: "I`m sorry, but only Phase 3 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Phoenix!",
            SuccessDialogText: "You`ve learned the Partner Dance, Phoenix!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 11,
            SpellId: 1_421,
            DanceName: "Melody",
            RequiredPhase: 6,
            PhaseGateText: "I`m sorry, but only Phase 6 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Melody!",
            SuccessDialogText: "You`ve learned the Partner Dance, Melody!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 10,
            SpellId: 1_422,
            DanceName: "Tipsy",
            RequiredPhase: 9,
            PhaseGateText: "I`m sorry, but only Phase 9 Performers can learn this dance. I can`t teach you this dance now.",
            PhaseGateOptionText: "Alright.",
            AlreadyLearnedText: "But you`ve already learned this dance, remember?",
            AlreadyLearnedOptionText: "Oh, alright.",
            SystemMessage: "You`ve learned the Partner Dance, Tipsy!",
            SuccessDialogText: "You`ve learned the Partner Dance, Tipsy!",
            SuccessOptionText: "Thanks."),
        new(
            OptionId: 8,
            SpellId: 1_415,
            DanceName: "Battle Dance",
            RequiredPhase: 1,
            PhaseGateText: "You can only learn it at Phase 1.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Battle Dance.",
            SuccessDialogText: "You`ve learned the Battle Dance. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 7,
            SpellId: 1_416,
            DanceName: "Triumph",
            RequiredPhase: 3,
            PhaseGateText: "You can only learn it at Phase 3.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Triumph.",
            SuccessDialogText: "You`ve learned the Triumph. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 6,
            SpellId: 1_417,
            DanceName: "Step Stomp",
            RequiredPhase: 5,
            PhaseGateText: "You can only learn it at Phase 5.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Step Stomp.",
            SuccessDialogText: "You`ve learned the Step Stomp. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 5,
            SpellId: 1_418,
            DanceName: "Moonlight",
            RequiredPhase: 7,
            PhaseGateText: "You can only learn it at Phase 7.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Moonlight.",
            SuccessDialogText: "You`ve learned the Moonlight. Hope you`ll work on it and learn more dances in the future. See you!",
            SuccessOptionText: "See you!"),
        new(
            OptionId: 4,
            SpellId: 1_419,
            DanceName: "Snow Wind",
            RequiredPhase: 9,
            PhaseGateText: "You can only learn it at Phase 9.",
            PhaseGateOptionText: "Oh.",
            AlreadyLearnedText: "You`ve already learned this dance.",
            AlreadyLearnedOptionText: "Oh.",
            SystemMessage: "You`ve learned the Snow Wind.",
            SuccessDialogText: "You`ve learned the Snow Wind. You are the master of dances now!",
            SuccessOptionText: "Yay!"),
    ];

    private static readonly LegacySubclassMasterDefinition[] LegacySubclassMasters =
    [
        new(
            LegacySubclassMasterApothecaryNpcId,
            LegacySubclassMasterApothecaryId,
            "NpcHandler.ApothecarySubClass",
            "NpcHandler.cs:12418-13058",
            113,
            "Apothecary",
            [
                "Hello! Please allow me to introduce the Apothecary sub-class. Apothecaries are masters of eastern herbs and can use their healing power to protect themselves from poison.",
                "Would you like to join us? After you join this sub-class, you can level it up and get promoted for stronger protection.",
            ],
            [
                new("Yes,~I`d~like~to~join!", 3),
                new("I~want~to~get~promoted.", 7),
                new("How~can~I~get~promoted?", 1),
                new("Okay,~I`ll~think~about~it.", LegacyNpcClosedOption),
            ],
            [
                "Apothecaries are very good at resisting poison damage. If you want to join us, reach Level 70 and pay 10 Meteors or 800 Study Points.",
                "After that, you can start your sub-class training on the Status sheet. Remember, a certain sub-class level is required to promote your sub-class phase.",
            ],
            [
                new("Pay~10~Meteors~to~join.", 6),
                new("Pay~800~Study~Points~to~join.", 4),
                new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 3,
            ItemJoinOptionId: 6,
            StudyPromptOptionId: 4,
            StudyConfirmOptionId: 5,
            PromotionRootOptionId: 7,
            PromotionFirstConfirmOptionId: 8,
            PromotionInfoOptionId: 1,
            StudyInfoOptionId: 2,
            ItemId: 1_088_001,
            ItemCount: 10,
            ItemName: "Meteors",
            JoinRequiredLevel: 70,
            PromotionEffectText: "further reduce poison damage"),
        new(
            LegacySubclassMasterMartialArtistNpcId,
            LegacySubclassMasterMartialArtistId,
            "NpcHandler.MartialArtist",
            "NpcHandler.cs:13060-13836",
            32,
            "Martial Artist",
            [
                "Hello there! You look quite capable. Perhaps you will make a great Martial Artist. The Martial Artist's awesome displays of combat prowess are a wonder to behold, but a nightmare to face.",
                "When you are a Martial Artist, you will be able to increase the chance of hitting with 1.5x Attack Power.",
                "Would you like to join us? After you join this sub-class, you can level it up and get promoted to increase your chance of hitting your enemies with more power.",
            ],
            [
                new("Yes,~I`d~like~to~join.", 3),
                new("I~want~to~get~promoted.", 7),
                new("How~can~I~get~promoted?", 1),
                new("Nice.~I`ll~think~about~it.", LegacyNpcClosedOption),
            ],
            [
                "As a Martial Artist, you will find your Critical Strike nicely enhanced. Make sure you have reached Level 70, and pay 5 Celestial Stones or 800 Study Points to join us.",
                "After that, you can work on your sub-class skill on the Status sheet. Remember, a certain sub-class level is required to promote the phase of your sub-class.",
            ],
            [
                new("Pay~5~Celestial~Stones~to~join.", 6),
                new("Pay~800~Study~Points~to~join.", 4),
                new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 3,
            ItemJoinOptionId: 6,
            StudyPromptOptionId: 4,
            StudyConfirmOptionId: 5,
            PromotionRootOptionId: 7,
            PromotionFirstConfirmOptionId: 8,
            PromotionInfoOptionId: 1,
            StudyInfoOptionId: 2,
            ItemId: 721_259,
            ItemCount: 5,
            ItemName: "Celestial Stones",
            JoinRequiredLevel: 70,
            PromotionEffectText: "increase your chance of making 1.5x Attack"),
        new(
            LegacySubclassMasterWarlockNpcId,
            LegacySubclassMasterWarlockId,
            "NpcHandler.Warlock",
            "NpcHandler.cs:13838-14561",
            23,
            "Warlock",
            [
                "Good day! Have you ever wondered what a Warlock is? Warlocks are masters of psionic energies and can increase their chance of casting spells with 2x Magical Attack.",
                "Isn't that wonderful? Would you like to join us? After you join this sub-class, you can level it up and get promoted to increase your power.",
            ],
            [
                new("Yes,~I`d~like~to~join.", 3),
                new("I~want~to~get~promoted.", 7),
                new("How~can~I~get~promoted?", 1),
                new("Not~my~concern.", LegacyNpcClosedOption),
            ],
            [
                "Warlocks are masters of psionic energies who have a chance to make Skill Critical Strike. If you have reached Level 70, pay 10 Bombs or 800 Study Points and you will be one of us.",
                "After that, you can enhance your sub-class skill on the Status sheet. Remember, a certain sub-class level is required to promote the phase of your sub-class.",
            ],
            [
                new("Pay~10~Bombs~to~join.", 6),
                new("Pay~800~Study~Points~to~join.", 4),
                new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 3,
            ItemJoinOptionId: 6,
            StudyPromptOptionId: 4,
            StudyConfirmOptionId: 5,
            PromotionRootOptionId: 7,
            PromotionFirstConfirmOptionId: 8,
            PromotionInfoOptionId: 1,
            StudyInfoOptionId: 2,
            ItemId: 721_261,
            ItemCount: 10,
            ItemName: "Bombs",
            JoinRequiredLevel: 70,
            PromotionEffectText: "increase your chance of casting with 2x Magical Attack"),
        new(
            LegacySubclassMasterSageNpcId,
            LegacySubclassMasterSageId,
            "NpcHandler.Sage",
            "NpcHandler.cs:14563-15206",
            87,
            "Sage",
            [
                "Hello, my friend. Are you here to join the Sage sub-class? Sages have strong spiritual powers and a fearsome ability to reduce a target's Magical Defense.",
                "This is very helpful in defeating those who might underestimate your power.",
                "Would you like to join us? After you join this sub-class, you can level it up and get promoted to reduce more of your target's Magical Defense.",
            ],
            [
                new("Yes,~I`d~like~to~join.", 3),
                new("I~want~to~get~promoted.", 7),
                new("How~can~I~get~promoted?", 1),
                new("Not~my~concern.", LegacyNpcClosedOption),
            ],
            [
                "Want to be a Sage? We are able to reduce a target's Magical Defense. Make sure you have reached Level 70, and pay 20 Lucky Amulets or 800 Study Points to join us.",
                "After being one of us, you can work on your sub-class training on the Status sheet. Remember, a certain sub-class level is required to promote the phase of your sub-class.",
            ],
            [
                new("Pay~20~Lucky~Amulets~to~join.", 6),
                new("Pay~800~Study~Points~to~join.", 4),
                new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 3,
            ItemJoinOptionId: 6,
            StudyPromptOptionId: 4,
            StudyConfirmOptionId: 5,
            PromotionRootOptionId: 7,
            PromotionFirstConfirmOptionId: 8,
            PromotionInfoOptionId: 1,
            StudyInfoOptionId: 2,
            ItemId: 723_087,
            ItemCount: 20,
            ItemName: "Lucky Amulets",
            JoinRequiredLevel: 70,
            PromotionEffectText: "reduce more of your target's Magical Defense"),
        new(
            LegacySubclassMasterChiMasterNpcId,
            LegacySubclassMasterChiMasterId,
            "NpcHandler.ChiMaster",
            "NpcHandler.cs:15208-15860",
            79,
            "Chi Master",
            [
                "Hello there! Please allow me to introduce the Chi Master sub-class. The Chi Master is a potent combatant so versed in counter attacks that they have a chance to prevent enemies from landing Critical Strikes.",
                "Would you like to join us? After you join this sub-class, you can level it up and get promoted to increase your immunity to critical strikes.",
            ],
            [
                new("Yes,~I`d~like~to~join~it.", 1),
                new("I~want~to~get~promoted.", 5),
                new("How~can~I~get~promoted?", 14),
                new("Where~to~buy~Yin-Yang~Fruits?", 16),
                new("Not~my~concern.", LegacyNpcClosedOption),
            ],
            [
                "The Chi Master focuses on immunity rate to critical strikes from enemies. If you have reached Level 70 and want to join us, give me 800 Study Points or a Yin-Yang Fruit.",
                "You can buy a Yin-Yang Fruit from the Horse Race Manager for 10,000 Horse Racing Points. After that, you can start your study of the sub-class on the Status sheet.",
                "Remember, a certain sub-class level is required to promote the phase of your sub-class.",
            ],
            [
                new("Pay~a~Yin-Yang~Fruit~to~join.", 4),
                new("Pay~800~Study~Points~to~join.", 2),
                new("Oh,~it~does~not~suit~me.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 1,
            ItemJoinOptionId: 4,
            StudyPromptOptionId: 2,
            StudyConfirmOptionId: 3,
            PromotionRootOptionId: 5,
            PromotionFirstConfirmOptionId: 6,
            PromotionInfoOptionId: 14,
            StudyInfoOptionId: 15,
            ItemId: 711_188,
            ItemCount: 1,
            ItemName: "Yin-Yang Fruit",
            JoinRequiredLevel: 70,
            PromotionEffectText: "increase your immunity rate to critical strikes"),
        new(
            LegacySubclassMasterLeadWranglerNpcId,
            LegacySubclassMasterWranglerId,
            "NpcHandler.LeadWrangler",
            "NpcHandler.cs:15862-16573",
            233,
            "Wrangler",
            [
                "Hey there! Let me tell you about the joys of the Wrangler. Wranglers are able to equip special armor on their mounts.",
                "As their Sub-Class level improves, their Max HP increases. Isn't it great? Would you like to join us?",
            ],
            [
                new("Yes,~I`d~like~to~join.", 1),
                new("I`d~like~to~get~promoted.", 7),
                new("How~can~I~get~promoted?", 5),
                new("Not~my~concern.", LegacyNpcClosedOption),
            ],
            [
                "Wranglers are able to equip special armor on their mounts. If you have reached Level 40, you can pay 40 Saddles or 800 Study Points to join us.",
                "After becoming a Wrangler, you can enhance your sub-class skill on the Status sheet. Remember, a certain sub-class level is required to promote the phase of your sub-class.",
            ],
            [
                new("Pay~40~Saddles~to~join.", 2),
                new("Pay~800~Study~Points~to~join.", 3),
                new("I~see.", LegacyNpcClosedOption),
            ],
            JoinOptionId: 1,
            ItemJoinOptionId: 2,
            StudyPromptOptionId: 3,
            StudyConfirmOptionId: 4,
            PromotionRootOptionId: 7,
            PromotionFirstConfirmOptionId: 8,
            PromotionInfoOptionId: 5,
            StudyInfoOptionId: 6,
            ItemId: 723_903,
            ItemCount: 40,
            ItemName: "Saddles",
            JoinRequiredLevel: 70,
            PromotionEffectText: "equip better mount armor"),
    ];

    internal static async ValueTask<bool> TryHandleLegacySubclassMastersNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacySubclassMasterPerformerNpcId)
        {
            return await TryHandleLegacyPerformerMasterAsync(context, session, optionId, cancellationToken)
                .ConfigureAwait(false);
        }

        var master = GetLegacySubclassMaster(npcId);
        if (master is null)
        {
            return false;
        }

        return await TryHandleLegacyStandardSubclassMasterAsync(context, session, master, optionId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyStandardSubclassMasterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubclassMasterDefinition master,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    master.RootTexts,
                    master.RootOptions,
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.PromotionRootOptionId)
        {
            await SendLegacySubclassMasterPromotionPromptAsync(context, session, master, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacySubclassPromotionPhase(master, optionId, out var promotionPhase))
        {
            await HandleLegacySubclassPromotionAsync(context, session, master, optionId, promotionPhase, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.JoinOptionId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    master.JoinTexts,
                    master.JoinOptions,
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.StudyPromptOptionId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    ["Are you sure you want to pay 800 Study Points to join us?"],
                    [new("Yes,~I`ve~decided!", master.StudyConfirmOptionId), new("No,~I~changed~my~mind.", LegacyNpcClosedOption)],
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.StudyConfirmOptionId)
        {
            await HandleLegacySubclassJoinSpendAsync(
                    context,
                    session,
                    master,
                    optionId,
                    LegacySubclassJoinSpendKind.StudyPoints,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.ItemJoinOptionId)
        {
            await HandleLegacySubclassJoinSpendAsync(
                    context,
                    session,
                    master,
                    optionId,
                    LegacySubclassJoinSpendKind.Item,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == master.PromotionInfoOptionId)
        {
            await SendLegacySubclassPromotionInfoAsync(context, master, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId == master.StudyInfoOptionId)
        {
            await SendLegacySubclassStudyInfoAsync(context, master.SourceMethod, master.AvatarId, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (master.NpcId == LegacySubclassMasterChiMasterNpcId && optionId == 16)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    ["You may pay 10,000 Horse Racing points to the Horse Race Manager to buy a Yin-Yang Fruit."],
                    [new("Thanks.", LegacyNpcClosedOption)],
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyPerformerMasterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    [
                        "Hello there! Please allow me to introduce the Performer sub-class. Performers are gifted in harmonizing song and swordplay into beautiful, unique dances.",
                        "These dances will surely make you the center of people's attention. After you join this sub-class, you can level it up and get promoted to learn more stylish dances.",
                        "Besides that, their physical and magical attacks improve when their Sub-Class level improves.",
                    ],
                    LegacyPerformerRootOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 40)
        {
            await SendLegacySubclassMasterPromotionPromptAsync(
                    context,
                    session,
                    CreateLegacyPerformerDefinition(),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId is >= 41 and <= 48)
        {
            var phase = checked((byte)(optionId - LegacyPerformerPromotionFirstOptionId + 2));
            await HandleLegacySubclassPromotionAsync(
                    context,
                    session,
                    CreateLegacyPerformerDefinition(),
                    optionId,
                    phase,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 20)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    [
                        "Performers always attract public attention with unique dances. If you have reached Level 70 and want to join us, bring me 15 Orchids or 800 Study Points.",
                        "A Performer Card is required and can be obtained by handing in 15 Orchids. After joining, open your character sheet and use the sub-class button to level up your sub-class.",
                    ],
                    LegacyPerformerJoinOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 23)
        {
            await HandleLegacyPerformerOrchidJoinIntroAsync(context, session, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId is 25 or 27)
        {
            await HandleLegacyPerformerCardSpendAsync(context, session, optionId, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId is 26 or 28 or 29)
        {
            await HandleLegacyPerformerOrchidSpendAsync(context, session, optionId, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId == 30)
        {
            await SendLegacyPerformerOrchidCountAsync(context, session, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (optionId == 21)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    ["Are you sure you want to pay 800 Study Points to join us?"],
                    LegacyPerformerStudyConfirmOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 22)
        {
            await HandleLegacySubclassJoinSpendAsync(
                    context,
                    session,
                    CreateLegacyPerformerDefinition(),
                    optionId,
                    LegacySubclassJoinSpendKind.StudyPoints,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 3)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    [
                        "It takes higher phases to learn more advanced dances. Learn Battle Dance at Phase 1, Triumph at Phase 3, Step Stomp at Phase 5, Moonlight at Phase 7, and Snow Wind at Phase 9.",
                        "Which of these dances do you want to learn?",
                    ],
                    LegacyPerformerDanceOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 9)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    ["The dance skill you can learn depends on your Performer Phase. The higher your phase is, the more dances you can learn. What kind of dance would you like to learn?"],
                    LegacyPerformerPartnerDanceOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyPerformerDance(optionId, out var dance))
        {
            await HandleLegacyPerformerDanceAsync(
                    context,
                    session,
                    dance,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await SendLegacySubclassPromotionInfoAsync(context, CreateLegacyPerformerDefinition(), cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            await SendLegacySubclassStudyInfoAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask HandleLegacySubclassJoinSpendAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubclassMasterDefinition master,
        byte optionId,
        LegacySubclassJoinSpendKind spendKind,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacySubClassState(session.Character);
        var entry = GetLegacySubclassEntry(state, master.SubClassId);
        if (entry?.Level >= 1)
        {
            await SendAlreadyJoinedDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (session.Character.Level < master.JoinRequiredLevel)
        {
            await SendJoinLevelGateDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (spendKind == LegacySubclassJoinSpendKind.StudyPoints)
        {
            if (state.StudyPoints < LegacySubclassMasterStudyCost)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        master.SourceMethod,
                        ["You do not have 800 Study Points to take the sub-class."],
                        LegacySubclassMasterAlrightOptions,
                        master.AvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            await ApplyLegacySubclassJoinAsync(
                    context,
                    session,
                    master,
                    optionId,
                    state,
                    LegacySubclassJoinSpendKind.StudyPoints,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, master.ItemId, master.ItemCount))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    [$"You do not have {master.ItemCount} {master.ItemName} to join this sub-class."],
                    LegacySubclassMasterOhOptions,
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await ApplyLegacySubclassJoinAsync(
                context,
                session,
                master,
                optionId,
                state,
                LegacySubclassJoinSpendKind.Item,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacySubclassJoinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubclassMasterDefinition master,
        byte optionId,
        LegacySubClassState state,
        LegacySubclassJoinSpendKind spendKind,
        CancellationToken cancellationToken)
    {
        var removedStudyPoints = spendKind == LegacySubclassJoinSpendKind.StudyPoints &&
            state.StudyPoints > LegacySubclassMasterStudyCost;
        var updatedState = state with
        {
            StudyPoints = removedStudyPoints
                ? checked((ushort)(state.StudyPoints - LegacySubclassMasterStudyCost))
                : state.StudyPoints,
            Entries = state.Entries
                .Concat([new LegacySubClassEntry(master.SubClassId, Level: 1, Phrase: 1)])
                .ToArray(),
        };
        var itemMutationFrames = new List<byte[]>();
        var updatedItems = session.PlayerItems;
        if (spendKind == LegacySubclassJoinSpendKind.Item)
        {
            updatedItems = ConsumeLegacyInventoryStack(
                session.PlayerItems,
                master.ItemId,
                master.ItemCount,
                itemMutationFrames);
        }

        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                session.Character,
                "SubProfInfo",
                FormatLegacySubClassState(updatedState)),
            "SubClassHashPoint",
            CalculateLegacySubClassHashPoint(updatedState).ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacySubclassJoinAsync(
                updatedContext,
                updatedSession,
                persistItems: spendKind == LegacySubclassJoinSpendKind.Item)
            .ConfigureAwait(false);
        context.Sessions?.Set(context.Connection, updatedSession);

        if (removedStudyPoints)
        {
            await context.Connection.SendAsync(
                    LegacyStringPacket.PlayerEffect(
                        CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                        "zf2-e300").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    new LegacySubClassGuiPacket(
                        LegacySubClassAction.Animation,
                        updatedState.StudyPoints,
                        StudyReceive: 0,
                        Entries: []).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (spendKind == LegacySubclassJoinSpendKind.Item)
        {
            foreach (var mutationFrame in itemMutationFrames)
            {
                await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
            }
        }

        await context.Connection.SendAsync(
                new LegacySubClassUpdatePacket(
                    LegacySubClassAction.LearnSubClass,
                    master.SubClassId,
                    Level: 1,
                    StudyReceive: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc subclass join " +
                $"{FormatSession(updatedContext)} source={master.SourceMethod} range={master.SourceRange} " +
                $"npcId={master.NpcId} option={optionId} spend={spendKind} " +
                $"subclassId={master.SubClassId} studyPoints={updatedState.StudyPoints} " +
                $"removedStudyPoints={removedStudyPoints}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacySubclassJoinAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool persistItems)
    {
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SubProfInfo"] = session.Character.Attributes["SubProfInfo"],
            ["SubClassHashPoint"] = session.Character.Attributes["SubClassHashPoint"],
        };
        if (!TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                session.Character.Uid,
                characterValues,
                out var characterPath,
                out var characterReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc subclass join character persistence " +
                    $"{FormatSession(context)} source=NpcHandler subclass state is session-only reason={characterReason}.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc subclass join character state " +
                    $"{FormatSession(context)} path={Path.GetFileName(characterPath)}.")
                .ConfigureAwait(false);
        }

        if (!persistItems)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc subclass join item persistence " +
                    $"{FormatSession(context)} source=NpcHandler reason=playerItemsRootMissing; items are session-only.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                session.Character.Uid,
                session.PlayerItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc subclass join item persistence " +
                    $"{FormatSession(context)} source=NpcHandler reason={ex.GetType().Name}; items are session-only.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask HandleLegacySubclassPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubclassMasterDefinition master,
        byte optionId,
        byte phase,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacySubClassState(session.Character);
        var entry = GetLegacySubclassEntry(state, master.SubClassId);
        if (!MeetsLegacySubclassPromotionRequirements(session, entry, phase))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    ["You have not met all the requirements for promotion yet."],
                    master.Title == "Wrangler" ? LegacySubclassMasterCloseOptions : LegacySubclassMasterOhOptions,
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacySubclassUnavailableAsync(
                context,
                master.NpcId,
                master.SourceMethod,
                master.SourceRange,
                optionId,
                $"SetPhrase({master.Title}, {phase}) promotion is not modeled safely in this NPC lane",
                "Promotion is not available yet. Your subclass was not changed.",
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyPerformerOrchidJoinIntroAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var master = CreateLegacyPerformerDefinition();
        var state = ReadLegacySubClassState(session.Character);
        if (GetLegacySubclassEntry(state, LegacySubclassMasterPerformerId)?.Level >= 1)
        {
            await SendAlreadyJoinedDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (session.Character.Level < master.JoinRequiredLevel)
        {
            await SendJoinLevelGateDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (HasLegacyInventoryStackCount(session.PlayerItems, LegacyPerformerCardItemId, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    ["I have accepted the 15 Orchids and here is your Performer Card. If you have reached level 70, you will become a Performer immediately. Are you sure you want to join us?"],
                    LegacyPerformerCardConfirmOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyPerformerOrchidMenuAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyPerformerCardSpendAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var master = CreateLegacyPerformerDefinition();
        var state = ReadLegacySubClassState(session.Character);
        if (GetLegacySubclassEntry(state, LegacySubclassMasterPerformerId)?.Level >= 1)
        {
            await SendAlreadyJoinedDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (session.Character.Level < master.JoinRequiredLevel)
        {
            await SendJoinLevelGateDialogAsync(context, master, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyPerformerCardItemId, 1))
        {
            await SendLegacyPerformerOrchidMenuAsync(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        await ApplyLegacySubclassJoinAsync(
                context,
                session,
                master,
                optionId,
                state,
                LegacySubclassJoinSpendKind.Item,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyPerformerOrchidSpendAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var (itemId, increment, missingText) = optionId switch
        {
            26 => (LegacyPerformerOneOrchidItemId, 1, "You don`t have an Orchid to give me."),
            28 => (LegacyPerformerThreeOrchidItemId, 3, " You don`t have 3 Orchids to give."),
            29 => (LegacyPerformerNineOrchidItemId, 9, "You don`t have 9 Orchids to give."),
            _ => (0u, 0, string.Empty),
        };

        var orchid = session.PlayerItems.InventoryItems.FirstOrDefault(
            item => item.ItemId == itemId && item.Bound == 0 && item.StackSize > 0);
        if (orchid is null)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyPerformerSourceMethod,
                    [missingText],
                    LegacySubclassMasterOhOptions,
                    LegacyPerformerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    "session-store-missing; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    "player-items-root-missing; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    "users-root-missing; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        var currentCount = ReadLegacyPerformerOrchidCount(session.Character);
        int creditedCount;
        try
        {
            creditedCount = checked(currentCount + increment);
        }
        catch (OverflowException)
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    "orchid-counter-overflow; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        var grantsCard = creditedCount >= LegacyPerformerOrchidCardThreshold;
        var remainingCount = grantsCard
            ? creditedCount - LegacyPerformerOrchidCardThreshold
            : creditedCount;

        PlayerItemSnapshotItem? performerCard = null;
        if (grantsCard)
        {
            if (!TryReadLegacyItemType(context.WorldData, LegacyPerformerCardItemId, out var performerCardType))
            {
                await LogLegacyPerformerOrchidFailureAsync(
                        context,
                        optionId,
                        itemId,
                        $"reward-itemtype-missing itemId={LegacyPerformerCardItemId}; no Orchid was consumed")
                    .ConfigureAwait(false);
                return;
            }

            if (performerCardType.Id != LegacyPerformerCardItemId ||
                performerCardType.LegacyDurability > ushort.MaxValue)
            {
                await LogLegacyPerformerOrchidFailureAsync(
                        context,
                        optionId,
                        itemId,
                        $"reward-itemtype-invalid itemId={LegacyPerformerCardItemId}; no Orchid was consumed")
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                performerCard = CreatePurchasedItems(
                    session.PlayerItems,
                    performerCardType,
                    count: 1,
                    isBound: false)[0];
            }
            catch (OverflowException)
            {
                await LogLegacyPerformerOrchidFailureAsync(
                        context,
                        optionId,
                        itemId,
                        "item-uid-exhausted; no Orchid was consumed")
                    .ConfigureAwait(false);
                return;
            }
        }

        var updatedItems = RemoveInventoryItem(session.PlayerItems, orchid);
        if (performerCard is not null)
        {
            if (!HasLegacyInventorySpace(updatedItems, 1))
            {
                await LogLegacyPerformerOrchidFailureAsync(
                        context,
                        optionId,
                        itemId,
                        "inventory-full-after-planned-Orchid-removal; no persistence or session mutation occurred")
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = updatedItems with
            {
                InventoryItems = updatedItems.InventoryItems
                    .Concat([performerCard])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        }

        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            LegacyPerformerOrchidCountAttribute,
            remainingCount.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };

        if (!TryPrepareLegacyPerformerOrchidCharacterWrite(
                updatedSession,
                out var characterPath,
                out var updatedCharacterLines,
                out var characterReason))
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    $"character-persistence-preflight-failed reason={characterReason}; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (!TryPrepareLegacyPerformerOrchidItemWrite(
                playerItemsRoot,
                updatedSession.Character.Uid,
                out var playerItemsPath,
                out var originalPlayerItemsBytes,
                out var itemReadReason))
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    $"item-persistence-preflight-failed reason={itemReadReason}; no persistence or session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        var updatedPlayerItemsBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedItems);
        if (!TryReplaceLegacyPerformerOrchidSnapshot(
                playerItemsPath,
                path => File.WriteAllBytes(path, updatedPlayerItemsBytes),
                out var itemWriteReason))
        {
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    $"item-persistence-write-failed reason={itemWriteReason}; no session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReplaceLegacyPerformerOrchidSnapshot(
                characterPath,
                path => File.WriteAllLines(path, updatedCharacterLines, LegacyCharacterSnapshotEncoding),
                out var characterWriteReason))
        {
            var restored = TryRestoreLegacyPerformerOrchidItemSnapshot(
                playerItemsPath,
                originalPlayerItemsBytes,
                out var restoreReason);
            await LogLegacyPerformerOrchidFailureAsync(
                    context,
                    optionId,
                    itemId,
                    $"character-persistence-write-failed reason={characterWriteReason}; " +
                    $"item-rollback={(restored ? "complete" : "failed:" + restoreReason)}; no session mutation occurred")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var removalFrame = new MsgItemUsagePacket(
            orchid.Uid,
            DwParam: 0,
            MsgItemUsageAction.RemoveInventory,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0).ToFrameBytes();
        await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> dialogTexts;
        IReadOnlyList<LegacyNpcDialogOption> dialogOptions;
        if (performerCard is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(performerCard).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            dialogTexts =
            [
                "Hmmm, I`ve accepted the 15 Orchids you just handed in and here is your Performer Card. Well, if you`ve reached level 70, you will become a Performer immediately. Are you sure you want to join us?",
            ];
            dialogOptions = LegacyPerformerEarnedCardConfirmOptions;
        }
        else
        {
            dialogTexts =
            [
                $"You`ve handed in {remainingCount} Orchids. As there are 15 Orchids, you can get a Performer Card.",
            ];
            dialogOptions = LegacySubclassMasterOhOptions;
        }

        await SendLegacyNpcDialogAsync(
                updatedContext,
                LegacyPerformerSourceMethod,
                dialogTexts,
                dialogOptions,
                LegacyPerformerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc Performer Orchid hand-in " +
                $"{FormatSession(updatedContext)} source={LegacyPerformerSourceMethod} " +
                $"sourceRange={LegacyPerformerOrchidSourceRange} option={optionId} " +
                $"orchidItemId={itemId} increment={increment} remaining={remainingCount} " +
                $"grantedPerformerCard={grantsCard.ToString().ToLowerInvariant()} " +
                "persisted-items-and-character-before-session-commit=true " +
                $"packetOrder=remove{(grantsCard ? "+add-card" : string.Empty)}+dialog.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPerformerOrchidCountAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var count = ReadLegacyPerformerOrchidCount(session.Character);
        var text = count >= 1
            ? $"You`ve handed in {count} Orchids. As you meet the requirement, you`ll get a Performer Card."
            : "You haven`t handed in any Orchids yet. It charges 15 Orchids for a Performer Card.";
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPerformerSourceMethod,
                [text],
                LegacySubclassMasterOhOptions,
                LegacyPerformerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static int ReadLegacyPerformerOrchidCount(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue(LegacyPerformerOrchidCountAttribute, out var rawCount) &&
            int.TryParse(rawCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
    }

    private static bool TryPrepareLegacyPerformerOrchidCharacterWrite(
        WorldSession session,
        out string path,
        out string[] updatedLines,
        out string reason)
    {
        path = string.Empty;
        updatedLines = [];
        reason = string.Empty;

        try
        {
            path = Path.Combine(
                session.UsersRoot!,
                session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            if (!File.Exists(path))
            {
                reason = "snapshot-missing";
                return false;
            }

            var lines = File.ReadAllLines(path, LegacyCharacterSnapshotEncoding);
            if (!TryValidateLegacyCharacterUid(lines, session.Character.Uid, out reason))
            {
                return false;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [LegacyPerformerOrchidCountAttribute] =
                    session.Character.Attributes[LegacyPerformerOrchidCountAttribute],
            };
            if (!TryApplyLegacyCharacterAttributeLines(lines, values, out updatedLines))
            {
                reason = "character-section-missing";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            reason = ex.GetType().Name;
            return false;
        }
    }

    private static bool TryPrepareLegacyPerformerOrchidItemWrite(
        string playerItemsRoot,
        long characterUid,
        out string path,
        out byte[]? originalBytes,
        out string reason)
    {
        path = string.Empty;
        originalBytes = null;
        reason = string.Empty;

        try
        {
            path = Path.Combine(
                playerItemsRoot,
                characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
            if (File.Exists(path))
            {
                originalBytes = File.ReadAllBytes(path);
            }

            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            reason = ex.GetType().Name;
            return false;
        }
    }

    private static bool TryReplaceLegacyPerformerOrchidSnapshot(
        string path,
        Action<string> writeTemporaryFile,
        out string reason)
    {
        reason = string.Empty;
        var temporaryPath = path + ".performer-orchid-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                reason = "snapshot-directory-missing";
                return false;
            }

            Directory.CreateDirectory(directory);
            writeTemporaryFile(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            reason = ex.GetType().Name;
            return false;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A failed best-effort temp cleanup must not commit the prepared session mutation.
            }
        }
    }

    private static bool TryRestoreLegacyPerformerOrchidItemSnapshot(
        string path,
        byte[]? originalBytes,
        out string reason)
    {
        if (originalBytes is not null)
        {
            return TryReplaceLegacyPerformerOrchidSnapshot(
                path,
                temporaryPath => File.WriteAllBytes(temporaryPath, originalBytes),
                out reason);
        }

        try
        {
            File.Delete(path);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            reason = ex.GetType().Name;
            return false;
        }
    }

    private static async ValueTask LogLegacyPerformerOrchidFailureAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        uint itemId,
        string reason)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc Performer Orchid hand-in " +
                $"{FormatSession(context)} source={LegacyPerformerSourceMethod} " +
                $"sourceRange={LegacyPerformerOrchidSourceRange} option={optionId} " +
                $"orchidItemId={itemId} reason={reason}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySubclassMasterPromotionPromptAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacySubclassMasterDefinition master,
        CancellationToken cancellationToken)
    {
        var state = ReadLegacySubClassState(session.Character);
        var phrase = GetLegacySubclassEntry(state, master.SubClassId)?.Phrase ?? 0;
        if (phrase < 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    [$"You are not a {master.Title} yet and cannot get promoted. Do you want to join the {master.Title} sub-class now?"],
                    [new("Yes,~I`d~like~to~join.", master.JoinOptionId), new("Oh,~not~now.", LegacyNpcClosedOption)],
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (phrase >= 9)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    master.SourceMethod,
                    ["There is nothing more I can teach you, now that you have reached the highest phase of this sub-class. Well done!"],
                    [new("Thank~you.", LegacyNpcClosedOption)],
                    master.AvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var nextPhase = checked((byte)(phrase + 1));
        var optionId = checked((byte)(master.PromotionFirstConfirmOptionId + nextPhase - 2));
        var confirmText = master.Title == "Wrangler" ? "Yup!" : "Positive.";
        await SendLegacyNpcDialogAsync(
                context,
                master.SourceMethod,
                [CreateLegacySubclassPromotionRequirementText(master, nextPhase)],
                [new(confirmText, optionId), new("Oh,~not~yet.", LegacyNpcClosedOption)],
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySubclassPromotionInfoAsync(
        WorldPacketHandlerContext context,
        LegacySubclassMasterDefinition master,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                master.SourceMethod,
                [
                    "Each Sub-Class has 9 phases. The higher the phase is, the more bonus effects the sub-class will offer.",
                    "These sub-classes also have a level system, and you need to level them up before you can get promoted. You can level up your sub-class by gaining Study Points.",
                ],
                [new("How~can~I~get~Study~Points?", master.StudyInfoOptionId), new("I~see.", LegacyNpcClosedOption)],
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySubclassStudyInfoAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [
                    "The only way to get those points is to read sub-class books, such as the Diligence Book.",
                    "When you have gained enough Study Points, open the sub-class sheet from the character panel and level up the sub-class there. Monsters in the wild may drop some books.",
                ],
                LegacySubclassMasterCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendAlreadyJoinedDialogAsync(
        WorldPacketHandlerContext context,
        LegacySubclassMasterDefinition master,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                master.SourceMethod,
                [$"Aha, you are already a {master.Title}, remember? Focus on upgrading your sub-class level now."],
                LegacySubclassMasterOhOptions,
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendJoinLevelGateDialogAsync(
        WorldPacketHandlerContext context,
        LegacySubclassMasterDefinition master,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                master.SourceMethod,
                [$"You will be invited to join us after you reach level {master.JoinRequiredLevel}."],
                LegacySubclassMasterOhOptions,
                master.AvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyPerformerOrchidMenuAsync(
        WorldPacketHandlerContext context,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                LegacyPerformerSourceMethod,
                ["How many Orchids would you like to hand in this time? It takes 15 Orchids to get a Performer Card, which is required to join the Performers."],
                LegacyPerformerOrchidOptions,
                LegacyPerformerAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySubclassUnavailableAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        string sourceMethod,
        string sourceRange,
        byte optionId,
        string reason,
        string dialogText,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    npcId,
                    optionId: optionId,
                    source: $"{sourceMethod} {sourceRange}",
                    reason: $"{reason}; source {sourceRange}"))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc SubclassMasters " +
                $"{FormatSession(context)} source={sourceMethod} range={sourceRange} npcId={npcId} option={optionId}; {reason}.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [dialogText],
                LegacySubclassMasterCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static LegacySubclassMasterDefinition? GetLegacySubclassMaster(uint npcId)
    {
        foreach (var master in LegacySubclassMasters)
        {
            if (master.NpcId == npcId)
            {
                return master;
            }
        }

        return null;
    }

    private static LegacySubclassMasterDefinition CreateLegacyPerformerDefinition()
    {
        return new LegacySubclassMasterDefinition(
            LegacySubclassMasterPerformerNpcId,
            LegacySubclassMasterPerformerId,
            LegacyPerformerSourceMethod,
            LegacyPerformerSourceRange,
            LegacyPerformerAvatarId,
            "Performer",
            [],
            [],
            [],
            [],
            JoinOptionId: 20,
            ItemJoinOptionId: 23,
            StudyPromptOptionId: 21,
            StudyConfirmOptionId: 22,
            PromotionRootOptionId: 40,
            PromotionFirstConfirmOptionId: LegacyPerformerPromotionFirstOptionId,
            PromotionInfoOptionId: 1,
            StudyInfoOptionId: 2,
            ItemId: LegacyPerformerCardItemId,
            ItemCount: 1,
            ItemName: "Performer Card",
            JoinRequiredLevel: 70,
            PromotionEffectText: "learn more dances");
    }

    private static LegacySubClassEntry? GetLegacySubclassEntry(LegacySubClassState state, byte subclassId)
    {
        foreach (var entry in state.Entries)
        {
            if (entry.Id == subclassId)
            {
                return entry;
            }
        }

        return null;
    }

    private static bool TryGetLegacySubclassPromotionPhase(
        LegacySubclassMasterDefinition master,
        byte optionId,
        out byte phase)
    {
        phase = 0;
        if (optionId < master.PromotionFirstConfirmOptionId ||
            optionId >= master.PromotionFirstConfirmOptionId + 8)
        {
            return false;
        }

        phase = checked((byte)(optionId - master.PromotionFirstConfirmOptionId + 2));
        return true;
    }

    private static bool MeetsLegacySubclassPromotionRequirements(
        WorldSession session,
        LegacySubClassEntry? entry,
        byte phase)
    {
        if (entry is null ||
            entry.Phrase != phase - 1 ||
            entry.Level < phase)
        {
            return false;
        }

        return session.Character.Level >= GetLegacySubclassPromotionRequiredLevel(phase) &&
            session.Character.Reborn >= GetLegacySubclassPromotionRequiredReborn(phase);
    }

    private static string CreateLegacySubclassPromotionRequirementText(
        LegacySubclassMasterDefinition master,
        byte phase)
    {
        var level = GetLegacySubclassPromotionRequiredLevel(phase);
        var reborn = GetLegacySubclassPromotionRequiredReborn(phase);
        var rebornText = reborn switch
        {
            1 => ", and have been reborn once",
            2 => ", and have been reborn twice",
            _ => string.Empty,
        };

        if (master.Title == "Wrangler")
        {
            return reborn == 0
                ? $"To get promoted to Phase {phase} and be able to equip better mount armor, your character should reach Level {level} and your Sub-Class reach Level {phase}. Have you met all these requirements?"
                : $"To get promoted to Phase {phase} and be able to equip better mount armor, you should be reborn {(reborn == 1 ? "once" : "twice")}, reach Character Level {level}, and have Sub-Class Level {phase}. Have you met all these requirements?";
        }

        return $"To get promoted to Phase {phase} and {master.PromotionEffectText}, you should have a character level of {level} and become a level {phase} {master.Title}{rebornText}. Have you met all these requirements?";
    }

    private static int GetLegacySubclassPromotionRequiredLevel(byte phase)
    {
        return phase switch
        {
            2 => 90,
            3 => 110,
            4 => 120,
            5 => 90,
            6 => 110,
            7 => 120,
            8 => 120,
            9 => 130,
            _ => int.MaxValue,
        };
    }

    private static int GetLegacySubclassPromotionRequiredReborn(byte phase)
    {
        return phase switch
        {
            5 or 6 or 7 => 1,
            8 or 9 => 2,
            _ => 0,
        };
    }

    private static bool TryGetLegacyPerformerDance(byte optionId, out LegacyPerformerDanceDefinition dance)
    {
        foreach (var candidate in LegacyPerformerDances)
        {
            if (candidate.OptionId == optionId)
            {
                dance = candidate;
                return true;
            }
        }

        dance = default!;
        return false;
    }

    private sealed record LegacyPerformerDanceDefinition(
        byte OptionId,
        ushort SpellId,
        string DanceName,
        byte RequiredPhase,
        string PhaseGateText,
        string PhaseGateOptionText,
        string AlreadyLearnedText,
        string AlreadyLearnedOptionText,
        string SystemMessage,
        string SuccessDialogText,
        string SuccessOptionText);

    private enum LegacySubclassJoinSpendKind
    {
        StudyPoints,
        Item,
    }

    private sealed record LegacySubclassMasterDefinition(
        uint NpcId,
        byte SubClassId,
        string SourceMethod,
        string SourceRange,
        ushort AvatarId,
        string Title,
        IReadOnlyList<string> RootTexts,
        IReadOnlyList<LegacyNpcDialogOption> RootOptions,
        IReadOnlyList<string> JoinTexts,
        IReadOnlyList<LegacyNpcDialogOption> JoinOptions,
        byte JoinOptionId,
        byte ItemJoinOptionId,
        byte StudyPromptOptionId,
        byte StudyConfirmOptionId,
        byte PromotionRootOptionId,
        byte PromotionFirstConfirmOptionId,
        byte PromotionInfoOptionId,
        byte StudyInfoOptionId,
        uint ItemId,
        uint ItemCount,
        string ItemName,
        int JoinRequiredLevel,
        string PromotionEffectText);
}
