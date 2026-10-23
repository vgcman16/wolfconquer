using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyPreparedNatureForceItem(
        WorldSession Session, PlayerItemSnapshotItem Item, ushort Mode, byte[] Frame);

    private static bool TryCommitLegacyNatureForceItem(
        WorldPacketHandlerContext context, WorldSession expected, ItemType type, uint itemId,
        bool stackReward, out LegacyPreparedNatureForceItem reward, out string reason)
    {
        reward = null!;
        reason = "nature-force-session-registration-missing-or-stale";
        if (!IsLegacyNatureForceSessionCurrent(context, expected)) return false;
        if (!TryPrepareLegacyNatureForceItem(context, expected, type, itemId, stackReward,
                out var prepared, out reason))
            return false;
        // This real commit is intentionally outside the preparation catch:
        // an observer exception after publication is not unchanged rejection.
        if (!context.Sessions!.TryCompareAndSet(context.Connection, expected, prepared.Session))
        {
            reason = "nature-force-session-changed-before-commit";
            return false;
        }
        reward = prepared;
        reason = string.Empty;
        return true;
    }

    private static bool TryPrepareLegacyNatureForceItem(
        WorldPacketHandlerContext context, WorldSession expected, ItemType type, uint itemId,
        bool stackReward, out LegacyPreparedNatureForceItem reward, out string reason)
    {
        reward = null!;
        reason = "nature-force-metadata-invalid";
        if (type is null || type.Id != itemId || !type.TryGetLegacyDurability(out _)) return false;
        try
        {
            PlayerItemSnapshot items;
            PlayerItemSnapshotItem item;
            ushort mode;
            if (stackReward)
                items = AddOrStackLegacyInventoryItem(expected.PlayerItems, type, itemId, 1, out item, out mode);
            else
                items = AddLegacyInventoryItem(expected.PlayerItems, type, 1, out item, out mode);
            var frame = LegacyGameItemPacket.FromSnapshotItem(item, mode: mode).ToFrameBytes();
            reward = new(expected with { PlayerItems = items }, item, mode, frame);
            reason = string.Empty;
            return true;
        }
        catch (OverflowException)
        {
            reason = "nature-force-preparation-overflow";
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            reason = "nature-force-frame-invalid";
            return false;
        }
    }

    private static bool IsLegacyNatureForceSessionCurrent(
        WorldPacketHandlerContext context, WorldSession expected) =>
        context.Sessions is { } sessions && sessions.TryGet(context.Connection, out var current) &&
        ReferenceEquals(current, expected);

    private static Task LogLegacyNatureForceRefusalAsync(
        WorldPacketHandlerContext context, int familyId, string reason) => context.Log.WriteLineAsync(
            "WorldServer refused legacy map3998 nature-force reward " +
            $"{FormatSession(context)} familyId={familyId} reason={reason}; no item grant or reward output committed.");

    private static async ValueTask LogLegacyNatureRuneThresholdAsync(
        WorldPacketHandlerContext updatedContext, LegacyMonsterInstance monster, Monster monsterFamily)
    {
        const uint questId = 3_636;
        const uint itemId = 3_008_752;
        await LogParityGapAsync(
                updatedContext,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "QuestGUI.SendAutoPatcher",
                    "map3998-rune-fragment-autopatcher",
                    reason: "source sends QuestGUI.SendAutoPatcher after collecting eight Rune Fragments for quest 3636; modern runtime grants/stacks the fragment but the optional client auto-route prompt is not modeled yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("itemId", itemId),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("questId", questId),
                        LegacyParityGapDiagnostics.Fact("itemId", itemId),
                    ]))
            .ConfigureAwait(false);
    }
}
