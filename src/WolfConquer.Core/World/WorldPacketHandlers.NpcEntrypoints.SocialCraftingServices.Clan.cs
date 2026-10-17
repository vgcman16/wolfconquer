namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySocialCraftingServicesClanNpcId = 5_533;

    private const string LegacySocialCraftingServicesClanSource = "NpcHandler.ClanNpc";

    private const string LegacySocialCraftingServicesClanLines = "52318-52674";

    private static async ValueTask<bool> TryHandleLegacySocialCraftingServicesClanAsync(
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
                        LegacySocialCraftingServicesClanSource,
                        [
                            "A Clan is a bond between its members. It can hold members and spouses, then share Battle Power bonuses after upgrades.",
                            "If you are ready, I can explain how to create one.",
                        ],
                        [new("How to create a Clan?", 1), new("I have no interest.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        [
                            "At level 50, you can create a Clan with 500,000 silver. Half becomes the Clan Fund.",
                            "You can recruit members and build your group, but you can only be in one Clan at a time.",
                        ],
                        [new("Create a Clan now.", 2), new("I am busy.", LegacyNpcClosedOption)],
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacySocialCraftingServicesInputDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        [
                            "To create a Clan, enter the name you want. The name must be 16 characters or fewer here.",
                        ],
                        "My Clan Name...",
                        inputOptionId: 3,
                        maxLength: 16,
                        avatarId: 6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 20:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        ["You can sign up with the Clan Guide once each day for Clan War. If you win, you receive a Normal Clan Pack."],
                        [new("Nice!", 21)],
                        39,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 21:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        ["Continuous occupation brings better prizes: Refined on day 7, Unique on day 14, Elite on day 21, and Super on day 28."],
                        [new("I will try my best!", LegacyNpcClosedOption)],
                        39,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 30:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        ["Only the Clan Leader can change the Clan name. The legacy source charges 1,075 CPs and applies the change after maintenance."],
                        [new("Deal!", 31), new("Wait a minute.", LegacyNpcClosedOption)],
                        39,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 31:
                await SendLegacySocialCraftingServicesInputDialogAsync(
                        context,
                        LegacySocialCraftingServicesClanSource,
                        ["Please enter a new name for your Clan."],
                        "Name:",
                        inputOptionId: 32,
                        maxLength: 16,
                        avatarId: 39,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 5:
            case 6:
            case 7:
            case 8:
            case 10:
            case 11:
            case 32:
                await SendLegacySocialCraftingServicesGapAsync(
                        context,
                        session,
                        LegacySocialCraftingServicesClanNpcId,
                        optionId,
                        LegacySocialCraftingServicesClanSource,
                        LegacySocialCraftingServicesClanLines,
                        "clan creation, rename, upgrade, BP upgrade, or disband requires persistent clan membership, funds, rank checks, and broadcasts",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
