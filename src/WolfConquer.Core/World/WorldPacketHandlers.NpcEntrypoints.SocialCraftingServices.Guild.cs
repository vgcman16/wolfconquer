namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesGuildCreatorNpcId = 10_003;

    private const string LegacySocialCraftingServicesGuildSource = "NpcHandler.GuildCreator";

    private const string LegacySocialCraftingServicesGuildLines = "52677-52910";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesGuildCreatorAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string input,
        CancellationToken cancellationToken)
    {
        _ = input;
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesGuildSource,
                        ["I handle Twin City guild matters, including guild creation, disbanding, renaming, and alliance operations."],
                        [
                            new("Create a guild.", 1),
                            new("Disband guild.", 2),
                            new("Rename guild.", 5),
                            new("Alliance operations.", 10),
                            new("Leave.", LegacyNpcClosedOption),
                        ],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacySocialCraftingServicesInputDialogAsync(
                        context,
                        LegacySocialCraftingServicesGuildSource,
                        ["Please enter the name you want to use for the guild."],
                        "Name",
                        inputOptionId: 3,
                        maxLength: 16,
                        avatarId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 5:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesGuildSource,
                        ["Only the Guild Leader can rename a guild. The legacy source charges CPs and updates guild, ally, and enemy records."],
                        [new("Agree.", 51), new("Wait a moment.", LegacyNpcClosedOption)],
                        120,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 51:
                await SendLegacySocialCraftingServicesInputDialogAsync(
                        context,
                        LegacySocialCraftingServicesGuildSource,
                        ["Please enter the guild's new name."],
                        "Name:",
                        inputOptionId: 52,
                        maxLength: 16,
                        avatarId: 110,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesGuildSource,
                        ["Alliance operations require guild rank, CTF rank, guild CP funds, and union state from the legacy server."],
                        [
                            new("Create an alliance.", 11),
                            new("Change alliance guild name.", 12),
                            new("Set kingdom title.", 13),
                            new("Leave.", LegacyNpcClosedOption),
                        ],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
            case 3:
            case 11:
            case 12:
            case 13:
            case 52:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesGuildCreatorNpcId,
                        optionId,
                        LegacySocialCraftingServicesGuildSource,
                        LegacySocialCraftingServicesGuildLines,
                        "guild creation, rename, disband, and alliance actions require persistent guild or union state, funds, rank checks, broadcasts, and client action windows",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
