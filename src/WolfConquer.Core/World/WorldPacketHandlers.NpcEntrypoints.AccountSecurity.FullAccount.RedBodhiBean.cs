using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyFullAccountRedBodhiBeanItemId = 3_009_104;
    private const int LegacyFullAccountRedBodhiBeanRequiredSpaces = 20;
    private const int LegacyFullAccountRedBodhiBeanSourceCalls = 10;
    private const ushort LegacyFullAccountRedBodhiBeanAmountPerCall = 1_000;
    private const int LegacyFullAccountRedBodhiBeanStackSizeFieldIndex = 46;
    private const string LegacyFullAccountRedBodhiBeanFullBagMessage =
        "Please make 20 more space in your inventory.";

    private static async ValueTask ApplyLegacyAccountSecurityFullAccountRedBodhiBeanAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        const byte optionId = 16;
        if (!HasLegacyInventorySpace(
                session.PlayerItems,
                LegacyFullAccountRedBodhiBeanRequiredSpaces))
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(LegacyFullAccountRedBodhiBeanFullBagMessage)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountSecurity FullAccount RedBodhiBean " +
                    $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
                    $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                    "reason=inventory-needs-20-spaces; source bag check precedes item metadata and persistence.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                    context,
                    "session-store-missing")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyFullAccountRedBodhiBeanItemId, out var itemType))
        {
            await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                    context,
                    context.WorldData is null ? "world-data-missing" : "itemtype-missing")
                .ConfigureAwait(false);
            return;
        }

        if (itemType.Id != LegacyFullAccountRedBodhiBeanItemId ||
            itemType.LegacyDurability > ushort.MaxValue ||
            !TryReadLegacyFullAccountRedBodhiBeanStackLimit(itemType, out var stackLimit))
        {
            await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                    context,
                    "itemtype-invalid")
                .ConfigureAwait(false);
            return;
        }

        var workingItems = session.PlayerItems;
        var mutations = new List<(PlayerItemSnapshotItem Item, ushort Mode)>(
            LegacyFullAccountRedBodhiBeanSourceCalls);
        for (var callIndex = 0; callIndex < LegacyFullAccountRedBodhiBeanSourceCalls; callIndex++)
        {
            var compatibleStack = workingItems.InventoryItems.FirstOrDefault(item =>
                item.ItemId == LegacyFullAccountRedBodhiBeanItemId &&
                item.Bound == 0 &&
                (uint)item.StackSize + LegacyFullAccountRedBodhiBeanAmountPerCall <= stackLimit);
            if (compatibleStack is not null)
            {
                var updatedStack = compatibleStack
                    .WithStackSize(checked((ushort)(
                        compatibleStack.StackSize + LegacyFullAccountRedBodhiBeanAmountPerCall)))
                    .WithLocked(0);
                workingItems = ReplaceInventoryItem(workingItems, compatibleStack, updatedStack);
                mutations.Add((updatedStack, LegacyGameItemPacket.UpdateItemMode));
                continue;
            }

            PlayerItemSnapshotItem grantedStack;
            try
            {
                grantedStack = CreatePurchasedItems(
                        workingItems,
                        itemType,
                        count: 1,
                        isBound: false)[0]
                    .WithStackSize(LegacyFullAccountRedBodhiBeanAmountPerCall)
                    .WithLocked(0)
                    .WithColor(checked((uint)Random.Shared.Next(3, 9)));
            }
            catch (OverflowException)
            {
                await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                        context,
                        "item-uid-exhausted")
                    .ConfigureAwait(false);
                return;
            }

            workingItems = workingItems with
            {
                InventoryItems = workingItems.InventoryItems
                    .Concat([grantedStack])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
            mutations.Add((grantedStack, LegacyGameItemPacket.AddItemMode));
        }

        byte[][] mutationFrames;
        try
        {
            mutationFrames = mutations
                .Select(mutation => LegacyGameItemPacket
                    .FromSnapshotItem(mutation.Item, mode: mutation.Mode)
                    .ToFrameBytes())
                .ToArray();
        }
        catch (OverflowException)
        {
            await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                    context,
                    "item-packet-invalid")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with { PlayerItems = workingItems };
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                persistItems: true,
                out var persistenceReason))
        {
            await LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
                    context,
                    persistenceReason)
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }

        var finalStacks = updatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == LegacyFullAccountRedBodhiBeanItemId && item.Bound == 0)
            .Select(item => item.StackSize.ToString(CultureInfo.InvariantCulture));
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountSecurity FullAccount RedBodhiBean " +
                $"{FormatSession(updatedContext)} source={LegacyAccountSecurityFullAccountSource} " +
                $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId={optionId} " +
                $"itemId={LegacyFullAccountRedBodhiBeanItemId} stackLimit={stackLimit} " +
                $"sourceCalls={LegacyFullAccountRedBodhiBeanSourceCalls} " +
                $"amountPerCall={LegacyFullAccountRedBodhiBeanAmountPerCall} totalAmount=10000 " +
                $"finalUnboundStacks={string.Join(',', finalStacks)} " +
                "packetOrder=GameItemAddOrUpdatex10.")
            .ConfigureAwait(false);
    }

    private static bool TryReadLegacyFullAccountRedBodhiBeanStackLimit(
        ItemType itemType,
        out ushort stackLimit)
    {
        stackLimit = 0;
        return itemType.Fields.Count > LegacyFullAccountRedBodhiBeanStackSizeFieldIndex &&
            ushort.TryParse(
                itemType.Fields[LegacyFullAccountRedBodhiBeanStackSizeFieldIndex],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out stackLimit) &&
            stackLimit >= LegacyFullAccountRedBodhiBeanAmountPerCall;
    }

    private static Task LogLegacyAccountSecurityFullAccountRedBodhiBeanFenceAsync(
        WorldPacketHandlerContext context,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc AccountSecurity FullAccount RedBodhiBean " +
            $"{FormatSession(context)} source={LegacyAccountSecurityFullAccountSource} " +
            $"sourceLines={LegacyAccountSecurityFullAccountSourceLines} optionId=16 " +
            $"itemId={LegacyFullAccountRedBodhiBeanItemId} reason={reason}; " +
            "no item, session state, or packet mutation was committed.");
    }
}
