using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyChanceRuneAsync(WorldPacketHandlerContext context,
        WorldSession session, MsgItemUsagePacket packet, CancellationToken cancellationToken)
    {
        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out _))
        {
            // Preserve the source's read-only refresh through the actual
            // current ranking owner; no intent, draw or success frame exists.
            UpdateLegacyRuneRanking(context, session);
            await context.Log.WriteLineAsync($"WorldServer skipped ChanceRune itemUid={packet.Id}; Rune was not found.").ConfigureAwait(false);
            return;
        }
        LegacyRuneChanceEvidence? issued = null;
        if (!TryCommitLegacyItemSpell(context, session, (stable, _, before) =>
            {
                issued ??= LegacyRuneChanceEvidence.Capture(context, stable, packet.Id, before);
                if (!issued.IsCurrent()) throw InvalidSpellMutation();
                return PrepareOwnedRuneChanceProjection(context, stable, issued);
            }, cancellationToken, out var updated, out var frames, out var identity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held ChanceRune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updated, frames, identity, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer committed ChanceRune itemUid={packet.Id}.").ConfigureAwait(false);
    }

    private static ItemSpellProjection PrepareOwnedRuneChanceProjection(WorldPacketHandlerContext context,
        WorldSession stable, LegacyRuneChanceEvidence evidence)
    {
        var projected = ProjectRecordedRuneChance(stable.Character, stable.PlayerItems, stable.PlayerSpells, evidence.Operation);
        if (projected.Character.MinHitPoints != stable.Character.MinHitPoints || !evidence.IsCurrent()) throw InvalidSpellMutation();
        var output = new List<byte[]>();
        foreach (var id in projected.RemovedSpells)
            output.Add(new MsgActionPacket
            { ObjId = CheckedUInt32(stable.Character.Uid, nameof(stable.Character.Uid)), DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes());
        output.Add(LegacyGameItemPacket.FromSnapshotItem(projected.Target,
            LegacyGameItemPacket.CreateRuneSpellOverride(projected.Target, context.WorldData!.ItemsById),
            LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
        var changed = stable with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        return new(changed, output.ToArray(), Refresh: null, UpdateRank: context.RuneRankings is not null, Chance: evidence);
    }

    private static bool RuneChanceOperationsEqual(LegacyRuneChanceOperation? first, LegacyRuneChanceOperation? second) =>
        first is null ? second is null : second is not null && first.SelectedUid == second.SelectedUid && first.Roll == second.Roll &&
        first.Metadata.SequenceEqual(second.Metadata) && first.Progress.SequenceEqual(second.Progress);
}
