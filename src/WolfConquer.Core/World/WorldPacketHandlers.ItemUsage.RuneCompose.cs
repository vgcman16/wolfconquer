using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyComposeRuneAsync(WorldPacketHandlerContext context,
        WorldSession session, MsgItemUsagePacket packet, PacketFrame frame, CancellationToken cancellationToken)
    {
        var supplied = ReadLegacyItemUsageArgs(frame);
        uint[] selected = [];
        if (packet.DwParam2 is > 0 and < 50)
        {
            if (supplied.Count < packet.DwParam2)
            {
                await context.Log.WriteLineAsync("WorldServer held ComposeRune reason=truncatedDeclaredMaterials; no success was published.").ConfigureAwait(false);
                return;
            }
            selected = supplied.Take(checked((int)packet.DwParam2)).ToArray();
        }
        if (!TryCommitLegacyItemSpell(context, session,
                (stable, _) => PrepareOwnedRuneComposeProjection(context, stable, packet.Id, selected),
                cancellationToken, out var updated, out var frames, out var identity, out var reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held ComposeRune reason={reason}; no success was published.").ConfigureAwait(false);
            return;
        }
        await PublishItemSpellFramesAsync(context, updated, frames, identity, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync($"WorldServer committed ComposeRune itemUid={packet.Id} selectedMaterials={selected.Length}.").ConfigureAwait(false);
    }

    private static ItemSpellProjection PrepareOwnedRuneComposeProjection(WorldPacketHandlerContext context,
        WorldSession stable, uint uid, uint[] selected)
    {
        var evidence = LegacyRuneComposeEvidence.Capture(context, stable, uid, selected);
        var projected = ProjectRecordedRuneCompose(stable.Character, stable.PlayerItems, stable.PlayerSpells, evidence.Operation);
        if (projected.Character.MinHitPoints != stable.Character.MinHitPoints || !evidence.IsCurrent()) throw InvalidSpellMutation();
        var output = new List<byte[]>();
        foreach (var item in projected.Events)
            output.Add(item.Kind switch
            {
                LegacyRuneComposeEventKind.RemoveSpell => new MsgActionPacket
                { ObjId = CheckedUInt32(stable.Character.Uid, nameof(stable.Character.Uid)), DwParam = item.Value, Type = (MsgActionType)109 }.ToFrameBytes(),
                LegacyRuneComposeEventKind.RemoveItem => new MsgItemUsagePacket(item.Value, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes(),
                _ => throw InvalidSpellMutation(),
            });
        output.Add(LegacyGameItemPacket.FromSnapshotItem(projected.Target,
            LegacyGameItemPacket.CreateRuneSpellOverride(projected.Target, context.WorldData!.ItemsById),
            LegacyGameItemPacket.UpdateItemMode).ToFrameBytes());
        var changed = stable with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        return new(changed, output.ToArray(), Refresh: null, UpdateRank: context.RuneRankings is not null, Compose: evidence);
    }

    private static bool RuneComposeOperationsEqual(LegacyRuneComposeOperation? first, LegacyRuneComposeOperation? second) =>
        first is null ? second is null : second is not null && first.SelectedUid == second.SelectedUid &&
        first.Materials.SequenceEqual(second.Materials) && first.Metadata.SequenceEqual(second.Metadata) &&
        first.Progress.SequenceEqual(second.Progress) && first.MaterialMetadata.SequenceEqual(second.MaterialMetadata);
}
