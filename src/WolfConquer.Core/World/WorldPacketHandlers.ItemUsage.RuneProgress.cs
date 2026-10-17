using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneProgressSource = "MsgItemUsuagePacket.RedRuneProgressControl";
    private const string LegacyRuneProgressLines = "1951-1979";

    private static async ValueTask HandleLegacyRedRuneProgressControlAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var rune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage red rune progress " +
                    $"{FormatSession(context)} source={LegacyRuneProgressSource} sourceLines={LegacyRuneProgressLines} " +
                    $"itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (packet.DwParam > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage red rune progress " +
                    $"{FormatSession(context)} source={LegacyRuneProgressSource} sourceLines={LegacyRuneProgressLines} " +
                    $"itemUid={packet.Id} dwParam={packet.DwParam}; source progress exceeds uint range.")
                .ConfigureAwait(false);
            return;
        }

        var socketProgress = CalculateLegacyRuneSocketProgress(
            rune,
            checked((uint)packet.DwParam),
            context.WorldData?.ItemsById);
        var updatedRune = rune.WithSocketProgress(socketProgress);
        var updatedSession = session with
        {
            PlayerItems = ReplaceRuneItem(session.PlayerItems, rune, updatedRune),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "red rune progress",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(
                        updatedRune,
                        LegacyGameItemPacket.CreateRuneSpellOverride(updatedRune, context.WorldData?.ItemsById),
                        LegacyGameItemPacket.UpdateItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage red rune progress " +
                $"{FormatSession(updatedContext)} source={LegacyRuneProgressSource} sourceLines={LegacyRuneProgressLines} " +
                $"itemUid={updatedRune.Uid} itemId={updatedRune.ItemId} dwParam={packet.DwParam} " +
                $"socketProgress={updatedRune.SocketProgress}.")
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyRuneSocketProgress(
        PlayerItemSnapshotItem rune,
        uint dwParam,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById)
    {
        if (!IsLegacyComprehensionRune(rune, itemsById))
        {
            return dwParam;
        }

        return dwParam switch
        {
            1 => 5_030,
            2 => 7_020,
            3 => 1_290,
            _ => 0,
        };
    }

    private static bool IsLegacyComprehensionRune(
        PlayerItemSnapshotItem rune,
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById)
    {
        if (rune.ItemId / 100 == 40_100)
        {
            return true;
        }

        if (rune.ItemId > int.MaxValue ||
            itemsById is null ||
            !itemsById.TryGetValue((int)rune.ItemId, out var itemTypes))
        {
            return false;
        }

        return itemTypes.Any(itemType =>
            itemType.Name.Contains("Comprehension", StringComparison.OrdinalIgnoreCase) ||
            itemType.Fields.Any(field => field.Contains("Comprehension", StringComparison.OrdinalIgnoreCase)));
    }
}
