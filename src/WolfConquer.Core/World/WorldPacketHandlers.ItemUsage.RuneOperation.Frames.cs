using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static ItemSpellProjection PrepareOwnedRuneProjection(WorldPacketHandlerContext context, WorldSession stable,
        object identity, bool equip, uint uid, ushort position, byte[] echo)
    {
        var evidence = LegacyRuneOperationEvidence.Capture(context, stable, equip, uid, position);
        var projected = ProjectRecordedRuneOperation(stable.Character, stable.PlayerItems, stable.PlayerSpells, evidence.Operation);
        var changed = stable with { Character = projected.Character, PlayerItems = projected.Items, PlayerSpells = projected.Spells };
        var actual = NormalizeItemSpellHitpoints(stable, ClampLegacyEquipmentRuntimeHitpoints(changed, context.WorldData));
        if (actual.Character.MinHitPoints != projected.Character.MinHitPoints || !evidence.IsCurrent()) throw InvalidSpellMutation();
        var output = new List<byte[]>();
        foreach (var item in projected.Events)
            output.Add(item.Kind switch
            {
                LegacyRuneOperationEventKind.Unequip => new MsgItemUsagePacket(item.Uid, item.Value,
                    MsgItemUsageAction.UnequipRune, 0, 0, 0, 0).ToFrameBytes(),
                LegacyRuneOperationEventKind.RemoveSpell => new MsgActionPacket
                { ObjId = CheckedUInt32(stable.Character.Uid, nameof(stable.Character.Uid)), DwParam = item.Value, Type = (MsgActionType)109 }.ToFrameBytes(),
                LegacyRuneOperationEventKind.GrantSpell => LegacySpellPacket.FromSnapshot(item.Spell!).ToFrameBytes(),
                _ => throw InvalidSpellMutation(),
            });
        var refresh = BuildItemSpellRefresh(context, actual, identity);
        output.Add(echo); output.AddRange(refresh);
        return new(actual, output.ToArray(), refresh, context.RuneRankings is not null, evidence);
    }

    private static bool RuneOperationsEqual(LegacyRuneOperation? first, LegacyRuneOperation? second) => first is null
        ? second is null : second is not null && first.Equip == second.Equip && first.SelectedUid == second.SelectedUid &&
            first.RequestedPosition == second.RequestedPosition && first.AlternateEquipmentActive == second.AlternateEquipmentActive &&
            first.BeastLevel == second.BeastLevel && first.BeastFlags == second.BeastFlags && first.BeastYellowSlots == second.BeastYellowSlots &&
            first.Metadata.SequenceEqual(second.Metadata) &&
            (first.Transform is null ? second.Transform is null : first.Transform.SameAs(second.Transform));
}
