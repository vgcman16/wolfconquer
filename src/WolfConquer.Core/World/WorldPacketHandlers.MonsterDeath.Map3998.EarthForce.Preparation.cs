using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyPreparedEarthForceReward(
        PlayerItemSnapshot Items, IReadOnlyList<byte[]> Frames, int FragmentsBefore,
        int FragmentsAfter, bool AddedFragment, bool Converted, ushort EarthForceItemMode);

    private static bool TryPrepareLegacyEarthForceReward(
        WorldPacketHandlerContext context, WorldSession session,
        out LegacyPreparedEarthForceReward reward, out string reason)
    {
        reward = null!;
        reason = "reward-preparation-overflow";
        try
        {
            return TryBuildLegacyEarthForceReward(context, session, out reward, out reason);
        }
        catch (OverflowException)
        {
            // Checked UID/durability/count failure is an unchanged refusal. No
            // state or native output has been published by this preparation.
            reason = "reward-preparation-overflow";
            return false;
        }
    }

    private static bool TryBuildLegacyEarthForceReward(
        WorldPacketHandlerContext context, WorldSession session,
        out LegacyPreparedEarthForceReward reward, out string reason)
    {
        const uint fragmentId = 3_008_754;
        const uint earthId = 3_008_745;
        const ushort required = 5;
        reward = null!;
        reason = "reward-metadata-invalid-or-missing";
        var before = CountLegacyInventoryItem(session.PlayerItems, fragmentId);
        var addFragment = before < required;
        var convert = before >= required - 1;
        ItemType? fragmentType = null;
        ItemType? earthType = null;
        if (addFragment && !TryReadLegacyEarthForceItemType(context, fragmentId, out fragmentType))
            return false;
        if (convert && !TryReadLegacyEarthForceItemType(context, earthId, out earthType))
            return false;

        var staged = session.PlayerItems;
        var frames = new List<byte[]>();
        if (addFragment)
        {
            staged = AddOrStackLegacyInventoryItem(staged, fragmentType!, fragmentId, 1,
                out var fragment, out var fragmentMode);
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(fragment, mode: fragmentMode).ToFrameBytes());
        }

        var after = CountLegacyInventoryItem(staged, fragmentId);
        ushort earthMode = 0;
        if (convert)
        {
            if (!TryConsumeLegacyInventoryItemCount(staged, fragmentId, required,
                    out var consumed, out var consumeFrames))
            {
                reason = "reward-fragment-cost-unavailable";
                return false;
            }
            staged = AddLegacyInventoryItem(consumed, earthType!, 1, out var earth, out earthMode);
            frames.AddRange(consumeFrames);
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(earth, mode: earthMode).ToFrameBytes());
        }

        reward = new(staged, frames.ToArray(), before, after, addFragment, convert, earthMode);
        reason = string.Empty;
        return true;
    }

    private static bool TryReadLegacyEarthForceItemType(
        WorldPacketHandlerContext context, uint id, out ItemType itemType)
    {
        return TryReadItemType(context.WorldData, id, out itemType) &&
            itemType is not null && itemType.Id == id && itemType.TryGetLegacyDurability(out _);
    }

    private static Task LogLegacyEarthForcePreparationRefusalAsync(
        WorldPacketHandlerContext context, string reason) => context.Log.WriteLineAsync(
            "WorldServer skipped legacy map 3998 Earth Force reward " +
            $"{FormatSession(context)} reason={reason}; no fragment debit, grant or reward output was committed.");
}
