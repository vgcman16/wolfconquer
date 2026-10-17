namespace WolfConquer.Core.World;

public sealed class LegacyTopInHoursInvitationScheduler
{
    private static readonly LegacyTopInHoursInvitation[] Invitations =
    [
        new(1, 6, "TopInHours [xx:01] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(4, 6, "TopInHours [xx:04] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(8, 6, "TopInHours [xx:08] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(12, 6, "TopInHours [xx:12] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(17, 6, "TopInHours [xx:17] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(22, 6, "TopInHours [xx:22] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(27, 6, "TopInHours [xx:27] has begun! Would you like to join?", 329, 304, 1002, 0, 60),
        new(32, 6, "TopInHours [xx:32] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(37, 6, "TopInHours [xx:37] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(42, 6, "TopInHours [xx:42] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(47, 6, "TopInHours [xx:47] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(52, 6, "TopInHours [xx:52] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(55, 6, "TopInHours [xx:55] has begun! Would you like to join?", 363, 304, 1002, 0, 60),
        new(2, 9, "TopInHours [The Grave] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(5, 9, "TopInHours [SkillTournament] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(8, 9, "TopInHours [Hero Of Server] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(11, 9, "TopInHours [DragonWar] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(14, 9, "TopInHours [KillerOfElite] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(18, 9, "TopInHours [LastManStand] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(22, 9, "TopInHours [CaptenServer] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(26, 9, "TopInHours [AngreMan] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(31, 9, "TopInHours [TopFight] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(35, 8, "TopInHours [DeathMatch] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(38, 8, "TopInHours [RaseInPase] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(41, 8, "TopInHours [OldMan] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(44, 8, "TopInHours [WaveBlade] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
        new(50, 7, "TopInHours [PK-PK] has begun! Would you like to join?", 372, 362, 1002, 0, 60),
    ];

    private readonly HashSet<string> _sentSlots = [];

    public IReadOnlyList<byte[]> CreateDueFrameBytes(DateTimeOffset now)
    {
        var updates = CreateDueUpdates(now);
        if (updates.Count == 0)
        {
            return [];
        }

        var frames = new byte[updates.Count][];
        for (var index = 0; index < updates.Count; index++)
        {
            frames[index] = updates[index].FrameBytes;
        }

        return frames;
    }

    public IReadOnlyList<LegacyRuntimeUpdate> CreateDueUpdates(DateTimeOffset now)
    {
        if (!TryGetInvitation(now, out var invitation))
        {
            return [];
        }

        var slotKey = CreateSlotKey(now, invitation);
        return _sentSlots.Add(slotKey)
            ? [new LegacyRuntimeUpdate(CreateMessage(invitation).ToFrameBytes())]
            : [];
    }

    public static bool TryGetInvitation(DateTimeOffset now, out LegacyTopInHoursInvitation invitation)
    {
        var local = now.LocalDateTime;
        foreach (var candidate in Invitations)
        {
            if (candidate.Minute == local.Minute && candidate.Second == local.Second)
            {
                invitation = candidate;
                return true;
            }
        }

        invitation = default!;
        return false;
    }

    public static bool TryGetInvitationForMinute(int minute, out LegacyTopInHoursInvitation invitation)
    {
        foreach (var candidate in Invitations)
        {
            if (candidate.Minute == minute)
            {
                invitation = candidate;
                return true;
            }
        }

        invitation = default!;
        return false;
    }

    public static LegacyMsgMessagePacket CreateMessage(LegacyTopInHoursInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return LegacyMsgMessagePacket.SystemCenter(" " + invitation.Name);
    }

    public static LegacyRuntimeMessageBoxPrompt CreateMessageBoxPrompt(LegacyTopInHoursInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new LegacyRuntimeMessageBoxPrompt(
            LegacyRuntimeMessageBoxPrompt.TopInHoursInvitationKey,
            invitation.Name,
            invitation.MapId,
            invitation.X,
            invitation.Y,
            invitation.DynamicId,
            invitation.Seconds);
    }

    private static string CreateSlotKey(DateTimeOffset now, LegacyTopInHoursInvitation invitation)
    {
        var local = now.LocalDateTime;
        return local.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) +
            "|" +
            invitation.Name;
    }
}

public sealed record LegacyTopInHoursInvitation(
    int Minute,
    int Second,
    string Name,
    ushort X,
    ushort Y,
    ushort MapId,
    ushort DynamicId,
    int Seconds);

public sealed record LegacyRuntimeUpdate(
    byte[] FrameBytes,
    LegacyRuntimeMessageBoxPrompt? MessageBoxPrompt = null,
    ushort? RuntimeStamina = null,
    ulong? RuntimeXpCount = null,
    bool? RuntimeXpListActive = null);

public sealed record LegacyRuntimeMessageBoxPrompt(
    string PendingKey,
    string Text,
    ushort MapId,
    ushort X,
    ushort Y,
    ushort DynamicId,
    int Seconds)
{
    public const string PendingAttribute = "PendingMessageBox";
    public const string DestinationMapIdAttribute = "PendingMessageBoxMapId";
    public const string DestinationXAttribute = "PendingMessageBoxX";
    public const string DestinationYAttribute = "PendingMessageBoxY";
    public const string DestinationDynamicIdAttribute = "PendingMessageBoxDynamicId";
    public const string TimeoutSecondsAttribute = "PendingMessageBoxSeconds";
    public const string TopInHoursInvitationKey = "TopInHoursInvitation";
    public const string DragonWarInvitationKey = "DragonWarInvitation";
    public const string FreezeWarInvitationKey = "FreezeWarInvitation";
}
