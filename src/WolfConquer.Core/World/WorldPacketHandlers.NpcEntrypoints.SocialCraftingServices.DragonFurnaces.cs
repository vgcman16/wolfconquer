namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesDragonFurnaceForgingNpcId = 23_737;
    private const uint LegacySocialCraftingServicesDragonFurnaceTwinNpcId = 23_736;
    private const uint LegacySocialCraftingServicesDragonFurnaceFusingNpcId = 23_735;

    private const string LegacySocialCraftingServicesDragonFurnaceForgingSource = "NpcHandler.DragonFurnaceForging";
    private const string LegacySocialCraftingServicesDragonFurnaceTwinSource = "NpcHandler.DragonFurnaceTwin";
    private const string LegacySocialCraftingServicesDragonFurnaceFusingSource = "NpcHandler.DragonFurnaceFusing";

    private const string LegacySocialCraftingServicesDragonFurnaceForgingLines = "54615-54732";
    private const string LegacySocialCraftingServicesDragonFurnaceTwinLines = "54735-54934";
    private const string LegacySocialCraftingServicesDragonFurnaceFusingLines = "54990-55014";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesDragonFurnaceForgingAsync(
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
                        LegacySocialCraftingServicesDragonFurnaceForgingSource,
                        [
                            "This ancient furnace contains energy to forge Anima.",
                            "After you put in an Anima, you can forge it up to 8 times in a row and stop at any time.",
                        ],
                        [new("Start forging.", 1), new("Tell me more.", 10)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceForgingSource,
                        ["Each Anima has 8 forging chances in the furnace. After each successful forging, you can continue or stop."],
                        [new("What is the success rate?", 11)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 11:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceForgingSource,
                        ["The legacy source says forging can upgrade an Anima phase, but failure destroys the Anima."],
                        [new("Start forging.", 0)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
            case 2:
            case 3:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesDragonFurnaceForgingNpcId,
                        optionId,
                        LegacySocialCraftingServicesDragonFurnaceForgingSource,
                        LegacySocialCraftingServicesDragonFurnaceForgingLines,
                        "Dragon Furnace forging removes or grants Anima items, rolls random forge outcomes, mutates furnace counters, and may teleport back to Twin City",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesDragonFurnaceTwinAsync(
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
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["This ancient furnace may enhance your Anima, but it is not always successful. Are you sure you want to use it?"],
                        [new("Yes.", 254), new("Get details on the official site.", 253)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 253:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        [$"Configured website: {WolfConquer.Core.Runtime.ConquerRuntimeDefaults.WebsiteUrl}"],
                        [new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 254:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["An alien world lies deep in the Dragon Furnace. You may forge, fuse, or smelt Animae there."],
                        [
                            new("Forge Anima.", 1),
                            new("Fuse Animae.", 100),
                            new("Smelt Anima.", 200),
                            new("I am not interested.", LegacyNpcClosedOption),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["Which floor would you like to visit and forge your Anima?"],
                        [
                            new("11th floor. (1 P11 Anima)", 11),
                            new("10th floor. (1 P10 Anima)", 10),
                            new("9th floor. (1 P9 Anima)", 9),
                            new("8th floor. (1 P8 Anima)", 8),
                            new("7th floor. (1 P7 Anima)", 7),
                            new("6th floor. (1 P6 Anima)", 6),
                            new("Next.", 50),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 50:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["Which floor would you like to visit and forge your Anima?"],
                        [
                            new("6th floor. (1 P6 Anima)", 6),
                            new("5th floor. (1 P5 Anima)", 5),
                            new("4th floor. (1 P4 Anima)", 4),
                            new("3rd floor. (1 P3 Anima)", 3),
                            new("Tell me more details.", 51),
                            new("Previous.", 1),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 51:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["Every Anima has 8 forging chances in the furnace, and you can stop after each successful forging."],
                        [new("What is the forging success rate?", 52)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 52:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["The legacy source says forging can upgrade an Anima phase, but failure destroys the Anima."],
                        [new("How to forge an Anima?", 51)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 100:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["To fuse Animae, you need two matching Animae and the Ancient Dragon's blood."],
                        [
                            new("11th floor. (2 P11 Animae)", 111),
                            new("10th floor. (2 P10 Animae)", 110),
                            new("9th floor. (2 P9 Animae)", 109),
                            new("8th floor. (2 P8 Animae)", 108),
                            new("7th floor. (2 P7 Animae)", 107),
                            new("6th floor. (2 P6 Animae)", 106),
                            new("Next.", 150),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 150:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["Which floor would you like to visit and fuse your Animae?"],
                        [
                            new("5th floor. (2 P5 Animae)", 105),
                            new("4th floor. (2 P4 Animae)", 104),
                            new("3rd floor. (2 P3 Animae)", 103),
                            new("2nd floor. (2 P2 Animae)", 102),
                            new("1st floor. (2 P1 Animae)", 101),
                            new("Tell me more details.", 151),
                            new("Previous.", 100),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 151:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["The legacy source says fusing has a 50 percent chance to create a higher-phase Anima and a 50 percent chance to fail."],
                        [new("Learn about other things.", 150), new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 200:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["The Solar and Lunar Furnaces can smelt P3 to P12 Anima. Only one can succeed in a round."],
                        [
                            new("Smelt P3 to P12 Anima.", 201),
                            new("Tell me more details.", 202),
                            new("Learn about other things.", 254),
                            new("I see.", LegacyNpcClosedOption),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 202:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["The fire in the twin furnaces never dies out, but smelting still depends on legacy event state."],
                        [
                            new("What is the smelting success rate?", 203),
                            new("Get details on the official site.", 253),
                            new("Learn about other things.", 200),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 203:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceTwinSource,
                        ["The legacy source says smelting has a 50 percent chance to upgrade an Anima and a 50 percent chance to fail."],
                        [new("Learn about other things.", 200), new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                if (optionId == 201 || optionId is >= 3 and < 13 || optionId is >= 101 and < 113)
                {
                    await SendLegacySocialCraftingServicesGapAsync(
                            context,
                            session,
                            LegacySocialCraftingServicesDragonFurnaceTwinNpcId,
                            optionId,
                            LegacySocialCraftingServicesDragonFurnaceTwinSource,
                            LegacySocialCraftingServicesDragonFurnaceTwinLines,
                            "Dragon Furnace entry removes or checks Anima items, creates dynamic map instances, spawns temporary furnace NPCs or Ancient Dragon monsters, and mutates furnace state",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesDragonFurnaceFusingAsync(
        WorldPacketHandlerContext context,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceFusingSource,
                        ["The Ancient Dragon's blood fuels the furnace for fusing two matching Animae into a better one. Fusing is not always successful."],
                        [new("Tell me more details.", 1), new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceFusingSource,
                        ["The Ancient Dragon's blood is required for fusing. Please note that the fusing is not always successful."],
                        [
                            new("What is the fusing success rate?", 2),
                            new("Get details on the official site.", 3),
                            new("Learn about other things.", 0),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceFusingSource,
                        ["There is a 50 percent chance of successfully fusing two matching Animae into a new higher-phase Anima."],
                        [new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesDragonFurnaceFusingSource,
                        ["Official details: https://co.99.com/guide/event/anima_introduction.shtml"],
                        [new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
