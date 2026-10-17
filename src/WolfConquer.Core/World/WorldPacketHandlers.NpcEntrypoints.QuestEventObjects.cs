namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPeachTreeNpcId = 7_997;
    private const uint LegacyPeachTree2NpcId = 7_996;
    private const uint LegacySquidwardOctopusNpcId = 9_534;
    private const uint LegacyClass6HouseNpcId = 18_950;

    private const ushort LegacySquidwardOctopusAvatarId = 60;
    private const ushort LegacyClass6HouseAvatarId = 111;
    private const ushort LegacyPeachTreeAvatarId = 7;

    private static readonly LegacyNpcDialogOption[] LegacyQuestEventOkayOptions =
    [
        new("I see.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyQuestEventObjectNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyPeachTreeNpcId => await TryHandleLegacyPeachTreeAsync(
                    context,
                    "NpcHandler.PeachTree",
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyPeachTree2NpcId => await TryHandleLegacyPeachTreeAsync(
                    context,
                    "NpcHandler.PeachTree2",
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacySquidwardOctopusNpcId => await TryHandleLegacySquidwardOctopusAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyClass6HouseNpcId => await TryHandleLegacyClass6HouseAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyPeachTreeAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        CancellationToken cancellationToken)
    {
        await SendLegacyQuestEventObjectGapAsync(
                context,
                sourceMethod,
                optionId,
                "Peach Tree picking is not available in this runtime yet.",
                "source checks Player.ActivePick and QuestGUI.IsActiveQuest(1011), then starts AddPick(\"Pick\", 5); local quest GUI and active-pick state are not modeled.",
                LegacyPeachTreeAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyQuestEventObjectGapAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string dialogText,
        string reason,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc QuestEventObject unavailable " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [dialogText],
                LegacyQuestEventOkayOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
