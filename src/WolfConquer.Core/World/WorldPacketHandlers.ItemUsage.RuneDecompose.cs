using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyDecomposeRuneAsync(WorldPacketHandlerContext context,
        WorldSession session, MsgItemUsagePacket packet, PacketFrame frame, CancellationToken cancellationToken)
    {
        var arguments = ReadLegacyItemUsageArgs(frame);
        if (packet.DwParam2 is 0 or >= 50 || arguments.Count < packet.DwParam2)
        {
            await context.Log.WriteLineAsync("WorldServer held DecomposeRune reason=missingOrTruncatedDeclaredArguments; no success was published.").ConfigureAwait(false);
            return;
        }
        uint uid = arguments[0]; // Actual source consumes args[0], not packet.Id.
        if (context.Trades is not { } trades)
        {
            await context.Log.WriteLineAsync("WorldServer held DecomposeRune reason=ownerUnavailable; no success was published.").ConfigureAwait(false);
            return;
        }
        WorldSession updated; byte[][] output; object identity; string reason; bool committed;
        // The existing inner lock is reentrant. Keep the deny-only issued
        // lifetime guard here too, so no second producer can reserve between
        // an abort return and its recovery fence. No transport/await is inside.
        lock (trades.SyncRoot)
        {
            LegacyRuneDecomposeEvidence? issued = null;
            try
            {
                committed = TryCommitLegacyItemSpell(context, session, (stable, _, before) =>
                {
                    issued ??= LegacyRuneDecomposeEvidence.Capture(context, stable, uid, before);
                    if (!issued.IsCurrent()) throw InvalidSpellMutation();
                    return PrepareOwnedRuneDecomposeProjection(context, stable, issued);
                }, cancellationToken, out updated, out output, out identity, out reason);
            }
            finally { issued?.FenceUnlessCommitted(); }
        }
        if (!committed)
        {
            await context.Log.WriteLineAsync($"WorldServer held DecomposeRune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updated, output, identity, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer committed DecomposeRune itemUid={uid}.").ConfigureAwait(false);
    }

    private static ItemSpellProjection PrepareOwnedRuneDecomposeProjection(WorldPacketHandlerContext context,
        WorldSession stable, LegacyRuneDecomposeEvidence evidence)
    {
        var projected = ProjectRecordedRuneDecompose(stable.Character, stable.PlayerItems, stable.PlayerSpells, evidence.Operation);
        if (projected.Character.MinHitPoints != stable.Character.MinHitPoints || !evidence.IsCurrent()) throw InvalidSpellMutation();
        var frames = new List<byte[]>();
        foreach (var id in projected.RemovedSpells)
            frames.Add(new MsgActionPacket { ObjId = CheckedUInt32(stable.Character.Uid, nameof(stable.Character.Uid)),
                DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes());
        frames.Add(new MsgItemUsagePacket(evidence.Operation.SelectedUid, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes());
        foreach (var essence in projected.Essences)
            frames.Add(LegacyGameItemPacket.FromSnapshotItem(essence, mode: LegacyGameItemPacket.AddItemMode).ToFrameBytes());
        frames.Add(LegacyGameItemPacket.FromSnapshotItem(projected.Fragment, mode: projected.FragmentMode).ToFrameBytes());
        return new(stable with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells },
            frames.ToArray(), Refresh: null, UpdateRank: context.RuneRankings is not null, Decompose: evidence);
    }

    private static bool RuneDecomposeOperationsEqual(LegacyRuneDecomposeOperation? first, LegacyRuneDecomposeOperation? second) =>
        first is null ? second is null : second is not null && first.SelectedUid == second.SelectedUid &&
        first.Progress == second.Progress && first.FragmentColor == second.FragmentColor &&
        first.RuneMetadata.SequenceEqual(second.RuneMetadata) && first.RewardMetadata.SequenceEqual(second.RewardMetadata) &&
        first.Request == second.Request && first.Allocation == second.Allocation;

    private static PlayerItemSnapshot RemoveLegacyRuneItem(PlayerItemSnapshot items, PlayerItemSnapshotItem rune) =>
        items with { RuneItems = items.RuneItems.Where(item => item.Uid != rune.Uid).ToArray() };
}
