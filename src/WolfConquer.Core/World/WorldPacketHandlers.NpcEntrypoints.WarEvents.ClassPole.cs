namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyClassPoleWarNpcId = 65_189;
    private const ushort LegacyClassPoleWarRootAvatarId = 268;
    private const ushort LegacyClassPoleWarClassAvatarId = 269;

    private static readonly LegacyNpcDialogOption[] LegacyClassPoleWarRootOptions =
    [
        new("Enter the match.", 1),
        new("Claim reward.", 50),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyClassPoleWarClassRewardOptions =
    [
        new("Archer.", 2),
        new("Warrior.", 3),
        new("Ninja.", 4),
        new("Monk.", 5),
        new("Trojan.", 6),
        new("Water Taoist.", 7),
        new("Fire Taoist.", 8),
        new("Pirate.", 9),
        new("Kung Fu King.", 10),
        new("Windwalker.", 11),
        new("Thunder Striker.", 13),
        new("Class Pole War.", 12),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    internal static async ValueTask<bool> TryHandleLegacyClassPoleWarNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string sourceMethod = "NpcHandler.ClassPoleWar";

        if (npcId != LegacyClassPoleWarNpcId)
        {
            return false;
        }

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Class PK runs every day from 22:00 to 22:30. Enter the arena, seize the battle pole, and claim your medal during the final five minutes."],
                    LegacyClassPoleWarRootOptions,
                    LegacyClassPoleWarRootAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            await TryEnterLegacyClassPoleWarAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 50)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    ["Choose the class reward you want to claim."],
                    LegacyClassPoleWarClassRewardOptions,
                    LegacyClassPoleWarClassAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (IsLegacyClassPoleWarRewardOption(optionId))
        {
            await TryClaimLegacyClassPoleWarRewardAsync(
                    context,
                    session,
                    sourceMethod,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static bool IsLegacyClassPoleWarRewardOption(byte optionId)
    {
        return optionId is >= 2 and <= 13;
    }
}
