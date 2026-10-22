using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRelicDecomposeCrystalItemId = 3_306_885;
    private const ushort LegacyRelicDecomposeCrystalStack = 9;
    private const string LegacyRelicDecomposeSource = "MsgItemUsuagePacket.DecomposeRelics";
    private const string LegacyRelicDecomposeLines = "2049-2068";

    private static async ValueTask HandleLegacyDecomposeRelicsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        if (!TryReadLegacyRelicDecomposeArgs(packet, frame, out var args))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage decompose relics " +
                    $"{FormatSession(context)}; declared argument body is incomplete (managed fail-closed).")
                .ConfigureAwait(false);
            return;
        }
        foreach (var itemUid in args)
        {
            if (!session.PlayerItems.InventoryItems.Any(item => item.Uid == itemUid))
            {
                await context.Log.WriteLineAsync(
                        "WorldServer skipped MsgItemUsage decompose relics " +
                        $"{FormatSession(context)} source={LegacyRelicDecomposeSource} " +
                        $"sourceLines={LegacyRelicDecomposeLines} missingItemUid={itemUid}; " +
                        "source requires every selected item to be in inventory before mutating.")
                    .ConfigureAwait(false);
                return;
            }
        }

        var updatedItems = session.PlayerItems;
        var mutationFrames = new List<byte[]>();
        var decomposedCount = 0;
        foreach (var itemUid in args)
        {
            // Native snapshots occurrences first and ignores both removal and Add return values.
            updatedItems = RemoveLegacyRelicDecomposeUnit(updatedItems, itemUid, mutationFrames);
            updatedItems = AddLegacyRelicDecomposeCrystals(context.WorldData, updatedItems, mutationFrames);
            decomposedCount++;
        }

        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "decompose relics",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        foreach (var mutationFrame in mutationFrames)
        {
            await context.Connection.SendAsync(mutationFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                CreateLegacyItemUsageFrameWithArgs(packet, args),
                cancellationToken)
            .ConfigureAwait(false);

        UpdateLegacyRuneRanking(updatedContext, updatedSession);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage decompose relics " +
                $"{FormatSession(updatedContext)} source={LegacyRelicDecomposeSource} " +
                $"sourceLines={LegacyRelicDecomposeLines} selected={args.Count} decomposed={decomposedCount} " +
                $"rewardItemId={LegacyRelicDecomposeCrystalItemId} rewardStack={LegacyRelicDecomposeCrystalStack}.")
            .ConfigureAwait(false);
    }

    private static byte[] CreateLegacyItemUsageFrameWithArgs(
        MsgItemUsagePacket packet,
        IReadOnlyList<uint> args)
    {
        var writer = new PacketWriter(
            WorldPacketOpcodes.MsgItemUsage,
            MsgItemUsagePacket.PayloadLength + (args.Count * sizeof(uint)));
        writer.WriteUInt32(0, packet.Id);
        writer.WriteUInt64(4, packet.DwParam);
        writer.WriteUInt16(12, (ushort)packet.Action);
        writer.WriteUInt32(14, packet.Timestamp);
        writer.WriteUInt32(18, packet.DwParam2);
        writer.WriteUInt32(22, packet.DwParam3);
        writer.WriteUInt32(26, packet.DwParam4);
        for (var index = 0; index < args.Count; index++)
        {
            writer.WriteUInt32(MsgItemUsagePacket.PayloadLength + (index * sizeof(uint)), args[index]);
        }

        return writer.ToArray();
    }
}
