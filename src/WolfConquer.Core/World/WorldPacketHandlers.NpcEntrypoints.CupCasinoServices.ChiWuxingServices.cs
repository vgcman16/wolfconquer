namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyCupCasinoServicesChiMakerNpcId = 554_432;
    private const uint LegacyCupCasinoServicesWuxingOvenNpcId = 35_016;

    private const ushort LegacyCupCasinoServicesChiMakerAvatarId = 63;

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerRootOptions =
    [
        new("Critical Strike + Max HP + Physical Attack + Immunity.", 5),
        new("Critical Strike + Max HP + Physical Attack + Counteraction.", 120),
        new("Breakthrough + Max HP + Physical Attack + Immunity.", 6),
        new("Magic Attack + Immunity + Skill Critical Strike + Max HP.", 50),
        new("Counteraction + Immunity + Physical Damage Reduction + Max HP.", 100),
        new("Which Chi set should I choose?", 15),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerDragonPhoenixTigerTurtleOptions =
    [
        new("Dragon.", 1),
        new("Phoenix.", 2),
        new("Tiger.", 3),
        new("Turtle.", 4),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerBreakthroughOptions =
    [
        new("Dragon.", 7),
        new("Phoenix.", 8),
        new("Tiger.", 9),
        new("Turtle.", 10),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerMagicOptions =
    [
        new("Dragon.", 51),
        new("Phoenix.", 52),
        new("Tiger.", 53),
        new("Turtle.", 54),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerCounteractionOptions =
    [
        new("Dragon.", 101),
        new("Phoenix.", 102),
        new("Tiger.", 103),
        new("Turtle.", 104),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesChiMakerCriticalCounterOptions =
    [
        new("Dragon.", 121),
        new("Phoenix.", 122),
        new("Tiger.", 123),
        new("Turtle.", 124),
        new("Not now.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesWuxingRootOptions =
    [
        new("Special blessed enchantment.", 1),
        new("How do I upgrade bonus level?", 14),
        new("What are enchanted effects?", 15),
        new("What are refinery effects?", 19),
        new("Not interested.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesWuxingBlessingOptions =
    [
        new("Garment.", 2),
        new("Mount Armor.", 4),
        new("Star Tower.", 6),
        new("Heaven Fan.", 8),
        new("Riding Crop.", 10),
        new("Gourd or Magic Bottle.", 12),
        new("Wing.", 50),
        new("Right weapon accessory.", 53),
        new("Left weapon accessory.", 54),
        new("I will think about it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCupCasinoServicesWuxingEnchantOptions =
    [
        new("How do I enchant?", 16),
        new("Can I enchant repeatedly?", 17),
        new("What gems do I need?", 18),
    ];

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandleChiMakerAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.ChiMaker";

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["I can perfect one Chi gate for 10,000 CPs. Which attribute set do you want?"],
                    LegacyCupCasinoServicesChiMakerRootOptions,
                    LegacyCupCasinoServicesChiMakerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 15)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [
                        "High battle-power players usually benefit most from Critical Strike.",
                        "Lower battle-power players may use Breakthrough to threaten stronger opponents.",
                    ],
                    LegacyCupCasinoServicesCloseOptions,
                    LegacyCupCasinoServicesChiMakerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyCupCasinoServicesTryGetChiMakerStageMenu(optionId, out var text, out var options))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    [text],
                    options,
                    LegacyCupCasinoServicesChiMakerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (LegacyCupCasinoServicesIsChiMakerMutationOption(optionId))
        {
            await LegacyCupCasinoServicesSendUnavailableAsync(
                    context,
                    source,
                    LegacyCupCasinoServicesGetChiMakerMutationLines(optionId),
                    npcId,
                    optionId,
                    "Chi rewriting is unavailable here until Chi gate state, CP persistence, and Chi attribute mutation are modeled safely.",
                    "spend 10,000 CPs and rewrite four Chi attributes on the selected gate",
                    LegacyCupCasinoServicesChiMakerAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool LegacyCupCasinoServicesTryGetChiMakerStageMenu(
        byte optionId,
        out string text,
        out IReadOnlyList<LegacyNpcDialogOption> options)
    {
        switch (optionId)
        {
            case 5:
                text = "Critical Strike + Max HP + Physical Attack + Immunity: choose a Chi gate.";
                options = LegacyCupCasinoServicesChiMakerDragonPhoenixTigerTurtleOptions;
                return true;
            case 6:
                text = "Breakthrough + Max HP + Physical Attack + Immunity: choose a Chi gate.";
                options = LegacyCupCasinoServicesChiMakerBreakthroughOptions;
                return true;
            case 50:
                text = "Magic Attack + Immunity + Skill Critical Strike + Max HP: choose a Chi gate.";
                options = LegacyCupCasinoServicesChiMakerMagicOptions;
                return true;
            case 100:
                text = "Counteraction + Immunity + Physical Damage Reduction + Max HP: choose a Chi gate.";
                options = LegacyCupCasinoServicesChiMakerCounteractionOptions;
                return true;
            case 120:
                text = "Critical Strike + Max HP + Physical Attack + Counteraction: choose a Chi gate.";
                options = LegacyCupCasinoServicesChiMakerCriticalCounterOptions;
                return true;
            default:
                text = string.Empty;
                options = [];
                return false;
        }
    }

    private static bool LegacyCupCasinoServicesIsChiMakerMutationOption(byte optionId)
    {
        return optionId is >= 1 and <= 4 ||
            optionId is >= 7 and <= 10 ||
            optionId is >= 51 and <= 54 ||
            optionId is >= 101 and <= 104 ||
            optionId is >= 121 and <= 124;
    }

    private static string LegacyCupCasinoServicesGetChiMakerMutationLines(byte optionId)
    {
        return optionId switch
        {
            >= 1 and <= 4 => "23776-23903",
            >= 7 and <= 10 => "23926-24053",
            >= 51 and <= 54 => "23476-23603",
            >= 101 and <= 104 => "23626-23753",
            >= 121 and <= 124 => "23326-23453",
            _ => "23267-24055",
        };
    }

    private static async ValueTask<bool> LegacyCupCasinoServicesTryHandleWuxingOvenAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.WuxingOven";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["A good set of equipment can help a lot in future battles. Is there something I can do for you?"],
                        LegacyCupCasinoServicesWuxingRootOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "For 10,000,000 CPs, the Wuxing Oven can attach one blessed attribute to specific equipment.",
                            "This is different from attributes upgraded with Tortoise Gems. Which equipment type would you like to bless?",
                        ],
                        LegacyCupCasinoServicesWuxingBlessingOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 14:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You can promote item bonus levels on the Forging interface by composing +n items. Higher +n stats make equipment more powerful."],
                        LegacyCupCasinoServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 15:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["An enchanted weapon can gain up to 255 HP. Higher-quality gems can add higher HP, and effects vary by gem."],
                        LegacyCupCasinoServicesWuxingEnchantOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 16:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Use the Forging interface. Put the weapon and enchantment materials in the required slots, then start enchanting."],
                        LegacyCupCasinoServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 17:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You can re-enchant on the Forging interface. If the new attribute is higher, it replaces the old one; otherwise, it stays unchanged."],
                        LegacyCupCasinoServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 18:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You can use Dragon, Phoenix, Tortoise, Rainbow, Moon, Violet, Fury, or Kylin gems. Effects vary by gem quality."],
                        LegacyCupCasinoServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 19:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You can obtain Composition Points from minor items and make main items reach the next bonus level on the Forging interface."],
                        [new LegacyNpcDialogOption("How do I refine equipment?", 20)],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "There are two ways to refine items.",
                            "1. Right-click a refined item and click equipment of the same type.",
                            "2. Refine the equipment on the Forging interface.",
                        ],
                        LegacyCupCasinoServicesCloseOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
            case 4:
            case 6:
            case 8:
            case 10:
            case 12:
            case 50:
                await LegacyCupCasinoServicesSendWuxingConfirmAsync(
                        context,
                        source,
                        LegacyCupCasinoServicesGetWuxingEquipmentName(optionId),
                        LegacyCupCasinoServicesGetWuxingConfirmOptionId(optionId),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 5:
            case 7:
            case 9:
            case 11:
            case 13:
            case 51:
            case 53:
            case 54:
                await LegacyCupCasinoServicesSendUnavailableAsync(
                        context,
                        source,
                        LegacyCupCasinoServicesGetWuxingMutationLines(optionId),
                        npcId,
                        optionId,
                        "Wuxing Oven blessing is unavailable here until equipment mutation, CP persistence, and item update packets are modeled safely.",
                        "spend 10,000,000 CPs and set Bless = 1 on the selected equipped item",
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask LegacyCupCasinoServicesSendWuxingConfirmAsync(
        WorldPacketHandlerContext context,
        string source,
        string equipmentName,
        byte yesOptionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source,
                [$"Are you sure you want to pay 10,000,000 CPs to add one blessed attribute to the {equipmentName}?"],
                [
                    new LegacyNpcDialogOption("Yes.", yesOptionId),
                    new LegacyNpcDialogOption("No, I changed my mind.", 1),
                    new LegacyNpcDialogOption("I need to think it over.", LegacyNpcClosedOption),
                ],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string LegacyCupCasinoServicesGetWuxingEquipmentName(byte optionId)
    {
        return optionId switch
        {
            2 => "Garment",
            4 => "Mount Armor",
            6 => "Star Tower",
            8 => "Heaven Fan",
            10 => "Riding Crop",
            12 => "Gourd",
            50 => "Wing",
            _ => "equipment",
        };
    }

    private static byte LegacyCupCasinoServicesGetWuxingConfirmOptionId(byte optionId)
    {
        return optionId switch
        {
            2 => 3,
            4 => 5,
            6 => 7,
            8 => 9,
            10 => 11,
            12 => 13,
            50 => 51,
            _ => LegacyNpcClosedOption,
        };
    }

    private static string LegacyCupCasinoServicesGetWuxingMutationLines(byte optionId)
    {
        return optionId switch
        {
            3 => "27362-27404",
            5 => "27305-27347",
            7 => "27256-27296",
            9 => "27199-27241",
            11 => "27142-27184",
            13 => "27078-27133",
            51 => "26905-26960",
            53 => "26974-27021",
            54 => "27022-27077",
            _ => "26816-27406",
        };
    }
}
