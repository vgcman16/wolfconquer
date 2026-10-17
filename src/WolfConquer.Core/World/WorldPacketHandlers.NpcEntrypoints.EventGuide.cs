namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyEventGuideNpcId = 5_125;
    private const ushort LegacyEventGuideAvatarId = 286;
    private const string LegacyEventGuideSourceMethod = "NpcHandler.Helpr";

    private static readonly LegacyNpcDialogOption[] LegacyEventGuideRootOptions =
    [
        new("Events by minute.", 1),
        new("Events by hour.", 2),
        new("Events by day.", 3),
        new("No, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyEventGuideMinuteOptions =
    [
        new("The Grave [xx:02-xx:04].", 4),
        new("Skill Tournament [xx:05-xx:06].", 4),
        new("Hero of the Server [xx:08-xx:10].", 4),
        new("Dragon War [xx:11-xx:13].", 4),
        new("Killer of Elite [xx:14-xx:17].", 4),
        new("Last Man Standing [xx:18-xx:21].", 4),
        new("Captain Server [xx:22-xx:25].", 4),
        new("Angry Man [xx:26-xx:30].", 4),
        new("Top Fight [xx:31-xx:34].", 4),
        new("Death Match [xx:35-xx:37].", 4),
        new("Race in Peace [xx:38-xx:40].", 4),
        new("Old Man [xx:41-xx:43].", 4),
        new("Satan [xx:45-xx:55].", 4),
        new("Wave Blade [xx:44-xx:47].", 4),
        new("PK-PK [xx:50-xx:55].", 4),
        new("Treasure Thief [xx:16].", 35),
        new("Crazy War [xx:01].", 37),
        new("Anima Shower [xx:33].", 38),
        new("Find the GM [xx:05].", 39),
        new("xx:01.", 5),
        new("xx:04.", 5),
        new("xx:08.", 5),
        new("xx:12.", 5),
        new("xx:17.", 5),
        new("xx:22.", 5),
        new("xx:27.", 5),
        new("xx:32.", 6),
        new("xx:37.", 6),
        new("xx:42.", 6),
        new("xx:47.", 6),
        new("xx:52.", 6),
        new("xx:55.", 6),
        new("Okay, thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyEventGuideHourOptions =
    [
        new("Nobility War [08:00-08:30].", 7),
        new("Triple War [02:00-02:30].", 8),
        new("Elite Guild War [20:00-20:30].", 9),
        new("War of Players [14:00-14:30].", 10),
        new("Champions of War [18:00-18:30].", 11),
        new("Top Pole [11:00-11:30].", 12),
        new("Clan War [20:30-21:00].", 13),
        new("Team PK [19:00-19:50].", 14),
        new("Elite PK [23:00-23:50].", 15),
        new("Skill Team PK [20:00-20:50].", 16),
        new("Pole Domination [15:00-16:00].", 17),
        new("Clan Domination [22:00-23:30].", 18),
        new("Emperor War [16:00-16:30].", 19),
        new("War Fighters [10:00-10:30].", 20),
        new("Weekly PK [13:00].", 21),
        new("Monthly PK [09:00-08:30].", 22),
        new("Mr. Conquer [06:00-06:10].", 23),
        new("Couples War [07:00-07:30].", 24),
        new("Skill Tournament [07:00-07:30].", 25),
        new("Football [16:30-16:40].", 26),
        new("Guild War Score [19:30-20:00].", 27),
        new("Clan War Score [16:30-16:59].", 28),
        new("Union War [22:30-22:59].", 29),
        new("Class PK War [23:30-23:59].", 30),
        new("Extreme PK [11:00-11:10].", 31),
        new("Class Pole War [22:00-22:30].", 32),
        new("Freeze War [03:30-03:40].", 33),
        new("Capture the Flag [21:00-22:00].", 34),
        new("Squidward Octopus [12:30-13:30].", 36),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyEventGuideDayOptions =
    [
        new("Guild War, Friday [21:00-22:00].", 40),
        new("Super Guild War, Tuesday [21:00-22:00].", 41),
    ];

    private static readonly IReadOnlyDictionary<byte, LegacyNpcTeleportDestination> LegacyEventGuideTeleports =
        new Dictionary<byte, LegacyNpcTeleportDestination>
        {
            [4] = new(1_002, 372, 362, LegacyEventGuideSourceMethod),
            [5] = new(1_002, 328, 304, LegacyEventGuideSourceMethod),
            [6] = new(1_002, 363, 304, LegacyEventGuideSourceMethod),
            [7] = new(1_002, 440, 339, LegacyEventGuideSourceMethod),
            [8] = new(1_002, 335, 278, LegacyEventGuideSourceMethod),
            [9] = new(1_002, 465, 314, LegacyEventGuideSourceMethod),
            [10] = new(1_002, 428, 326, LegacyEventGuideSourceMethod),
            [11] = new(1_002, 478, 324, LegacyEventGuideSourceMethod),
            [12] = new(1_002, 465, 325, LegacyEventGuideSourceMethod),
            [13] = new(1_002, 430, 272, LegacyEventGuideSourceMethod),
            [14] = new(1_002, 443, 272, LegacyEventGuideSourceMethod),
            [15] = new(1_002, 425, 270, LegacyEventGuideSourceMethod),
            [16] = new(1_002, 431, 271, LegacyEventGuideSourceMethod),
            [17] = new(1_002, 334, 341, LegacyEventGuideSourceMethod),
            [18] = new(1_002, 472, 325, LegacyEventGuideSourceMethod),
            [19] = new(1_002, 444, 339, LegacyEventGuideSourceMethod),
            [20] = new(1_002, 327, 340, LegacyEventGuideSourceMethod),
            [21] = new(1_002, 424, 280, LegacyEventGuideSourceMethod),
            [22] = new(1_002, 365, 342, LegacyEventGuideSourceMethod),
            [23] = new(1_002, 319, 255, LegacyEventGuideSourceMethod),
            [24] = new(1_002, 363, 334, LegacyEventGuideSourceMethod),
            [25] = new(1_002, 326, 212, LegacyEventGuideSourceMethod),
            [26] = new(1_002, 324, 336, LegacyEventGuideSourceMethod),
            [27] = new(1_002, 333, 248, LegacyEventGuideSourceMethod),
            [28] = new(1_002, 451, 325, LegacyEventGuideSourceMethod),
            [29] = new(1_002, 363, 337, LegacyEventGuideSourceMethod),
            [30] = new(1_002, 450, 297, LegacyEventGuideSourceMethod),
            [31] = new(1_002, 455, 301, LegacyEventGuideSourceMethod),
            [32] = new(1_002, 453, 337, LegacyEventGuideSourceMethod),
            [33] = new(1_002, 355, 250, LegacyEventGuideSourceMethod),
            [34] = new(1_002, 322, 322, LegacyEventGuideSourceMethod),
            [35] = new(1_002, 450, 366, LegacyEventGuideSourceMethod),
            [36] = new(1_002, 461, 352, LegacyEventGuideSourceMethod),
            [37] = new(1_002, 486, 325, LegacyEventGuideSourceMethod),
            [38] = new(1_002, 326, 333, LegacyEventGuideSourceMethod),
            [39] = new(1_002, 421, 373, LegacyEventGuideSourceMethod),
            [40] = new(1_002, 324, 321, LegacyEventGuideSourceMethod),
            [41] = new(1_002, 344, 250, LegacyEventGuideSourceMethod),
        };

    internal static async ValueTask<bool> TryHandleLegacyEventGuideNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyEventGuideNpcId)
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyEventGuideSourceMethod,
                        ["Hello. I can guide you to the server's events and wars."],
                        LegacyEventGuideRootOptions,
                        LegacyEventGuideAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyEventGuideSourceMethod,
                        ["These events run every few minutes."],
                        LegacyEventGuideMinuteOptions,
                        LegacyEventGuideAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyEventGuideSourceMethod,
                        ["These events run by the hour."],
                        LegacyEventGuideHourOptions,
                        LegacyEventGuideAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyEventGuideSourceMethod,
                        ["These events run on specific days."],
                        LegacyEventGuideDayOptions,
                        LegacyEventGuideAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }

        if (!LegacyEventGuideTeleports.TryGetValue(optionId, out var destination))
        {
            return false;
        }

        await SendLegacyNpcTeleportAsync(
                context,
                session,
                npcId,
                optionId,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
