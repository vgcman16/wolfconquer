namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesSolarFurnaceNpcId = 23_580;
    private const uint LegacySocialCraftingServicesLunarFurnaceNpcId = 23_581;

    private const string LegacySocialCraftingServicesSolarFurnaceSource = "NpcHandler.SolarFurnace";
    private const string LegacySocialCraftingServicesLunarFurnaceSource = "NpcHandler.LunarFurnace";

    private const string LegacySocialCraftingServicesSolarFurnaceLines = "55017-55078";
    private const string LegacySocialCraftingServicesLunarFurnaceLines = "55081-55141";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesSmeltingFurnaceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        string sourceLines,
        string furnaceName,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [$"The {furnaceName} furnace can smelt Anima. Only one twin furnace can succeed in a smelting round."],
                        [
                            new("P12 Anima.", 12),
                            new("P11 Anima.", 11),
                            new("P10 Anima.", 10),
                            new("P9 Anima.", 9),
                            new("P8 Anima.", 8),
                            new("P7 Anima.", 7),
                            new("P6 Anima.", 6),
                            new("Next.", 13),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 13:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [$"The {furnaceName} furnace can smelt lower-phase Anima too."],
                        [
                            new("P5 Anima.", 5),
                            new("P4 Anima.", 4),
                            new("P3 Anima.", 3),
                            new("Tell me more details.", 14),
                            new("Previous.", 0),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 14:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["The twin furnaces were built by legendary blacksmiths, and their fire is still fierce."],
                        [
                            new("What is the smelting success rate?", 15),
                            new("Get details on the official site.", 16),
                            new("Learn about other things.", 13),
                        ],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 15:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        ["The legacy source says smelting has a 50 percent chance to upgrade an Anima and a 50 percent chance to fail."],
                        [new("Learn about other things.", 14), new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 16:
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [furnaceName == "solar" ? "Official details: https://co.99.com/guide/event/anima_introduction.shtml" : $"Configured website: {WolfConquer.Core.Runtime.ConquerRuntimeDefaults.WebsiteUrl}"],
                        [new("I see.", LegacyNpcClosedOption)],
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                if (optionId is >= 3 and < 13)
                {
                    await SendLegacySocialCraftingServicesGapAsync(
                            context,
                            session,
                            npcId,
                            optionId,
                            sourceMethod,
                            sourceLines,
                            "furnace smelting removes Anima items, mutates DragonFurnace state, and depends on global furnace round state",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                return false;
        }
    }
}
