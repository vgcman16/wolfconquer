namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWhiteDragonNpcId = 25_004;
    private const uint LegacyWhiteDragonRosewoodIncenseItemId = 3_007_565;
    private const ushort LegacyWhiteDragonAvatarId = 226;
    private const string LegacyWhiteDragonSource = "NpcHandler.WhiteDragon";

    private static readonly LegacyNpcDialogOption[] LegacyWhiteDragonRootOptions =
    [
        new("Swap~incense~for~gifts.", 1),
        new("I'll~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyWhiteDragonExchangeOptions =
    [
        new("Infernal~Echo~Scroll~(20).", 2),
        new("Grace~of~Heaven~Scroll~(10).", 3),
        new("Wrath~of~the~Emperor~Scroll~(1).", 4),
        new("2000~Chi~Points~(10).", 5),
        new("I'll~think~about~it.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyWhiteDragonAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacyWhiteDragonNpcId)
        {
            return false;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyWhiteDragonSource,
                        [
                            "Burn Rosewood Incense and you will feel refreshed and clear-minded.",
                            "I can exchange incense for monk skill books or Chi points.",
                        ],
                        LegacyWhiteDragonRootOptions,
                        LegacyWhiteDragonAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyWhiteDragonSource,
                        [
                            "Different gifts require different amounts of Rosewood Incense.",
                            "Choose Infernal Echo, Grace of Heaven, Wrath of the Emperor, or Chi Points.",
                        ],
                        LegacyWhiteDragonExchangeOptions,
                        LegacyWhiteDragonAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await TryExchangeLegacyWhiteDragonIncenseAsync(
                        context,
                        session,
                        optionId,
                        requiredIncense: 20,
                        rewardItemId: 3_007_568,
                        missingText: "Please come back when you have 20 Rosewood Incense in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await TryExchangeLegacyWhiteDragonIncenseAsync(
                        context,
                        session,
                        optionId,
                        requiredIncense: 10,
                        rewardItemId: 3_007_567,
                        missingText: "Please come back when you have 10 Rosewood Incense in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
                await TryExchangeLegacyWhiteDragonIncenseAsync(
                        context,
                        session,
                        optionId,
                        requiredIncense: 1,
                        rewardItemId: 3_007_566,
                        missingText: "Please come back when you have 1 Rosewood Incense in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 5:
                if (HasLegacyInventoryStackCount(session.PlayerItems, LegacyWhiteDragonRosewoodIncenseItemId, 10))
                {
                    await TryExchangeLegacyWhiteDragonIncenseAsync(
                            context,
                            session,
                            optionId,
                            requiredIncense: 10,
                            rewardItemId: 3_005_128,
                            missingText: "Please come back when you have 5 Rosewood Incense in your inventory.",
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await TryExchangeLegacyWhiteDragonIncenseAsync(
                        context,
                        session,
                        optionId,
                        requiredIncense: 1,
                        rewardItemId: 3_007_566,
                        missingText: "Please come back when you have 5 Rosewood Incense in your inventory.",
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask TryExchangeLegacyWhiteDragonIncenseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint requiredIncense,
        uint rewardItemId,
        string missingText,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyInventoryStackCount(session.PlayerItems, LegacyWhiteDragonRosewoodIncenseItemId, requiredIncense))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyWhiteDragonSource,
                    [missingText],
                    [new LegacyNpcDialogOption("Oh.", LegacyNpcClosedOption)],
                    LegacyWhiteDragonAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var consumeFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(
            session.PlayerItems,
            LegacyWhiteDragonRosewoodIncenseItemId,
            requiredIncense,
            consumeFrames);
        if (!HasLegacyInventorySpace(updatedItems, 1))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacyWhiteDragonSource,
                    ["Please make one more space in your inventory."],
                    [new LegacyNpcDialogOption("Oh.", LegacyNpcClosedOption)],
                    LegacyWhiteDragonAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var rewardItem = CreateLegacyNpcItemGrant(context, updatedItems, rewardItemId);
        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems.Concat([rewardItem]).OrderBy(item => item.Uid).ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyNpcItemsAsync(updatedContext, rewardItemId, LegacyWhiteDragonSource).ConfigureAwait(false);
        foreach (var frame in consumeFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket.FromSnapshotItem(rewardItem).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyItemFollowupsAsync(updatedContext, rewardItem, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc WhiteDragon exchange " +
                $"{FormatSession(updatedContext)} source={LegacyWhiteDragonSource} optionId={optionId} " +
                $"requiredIncense={requiredIncense} rewardItemId={rewardItemId}.")
            .ConfigureAwait(false);
    }
}
