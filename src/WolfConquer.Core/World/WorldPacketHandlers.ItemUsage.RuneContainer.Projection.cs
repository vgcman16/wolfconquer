using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneOperationProjection ProjectRecordedRuneContainer(CharacterSnapshot character,
        PlayerItemSnapshot before, PlayerSpellSnapshot spells, LegacyRuneContainerOperation operation,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        operation.Validate(semantics);
        if (!TryGetRuneItem(before, operation.SelectedUid, out var selected)) throw InvalidSpellMutation();
        var metadata = RequireRecordedRuneMetadata(before, selected, operation.Metadata, semantics);
        LegacyRuneSpellOrigins.RequireRows(character, spells);
        var origins = LegacyRuneSpellOrigins.Read(character);
        var providersBefore = RuneProviders(before, metadata, semantics);
        var rows = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells).Spells.ToList();
        var output = new List<LegacyRuneOperationEvent>();
        RetireLegacyPassiveRuneOrigins(before, metadata, providersBefore, origins, rows, output, semantics);
        if (origins.Keys.Any(id => !providersBefore.Contains(id))) throw InvalidSpellMutation();
        var items = before;
        switch (operation.Kind)
        {
            case LegacyRuneContainerKind.Store:
                if (character.Reborn < 2) throw InvalidSpellMutation();
                // A same-base storage row, including this already-stored row,
                // leaves the source operation unchanged but still responds.
                if (!HasRuneStorageSameBase(before, selected))
                    items = ReplaceRuneItem(before, selected, selected.WithPosition(LegacyRuneStoragePosition));
                break;
            case LegacyRuneContainerKind.Bag:
                if (character.Reborn < 2 || selected.Position != LegacyRuneBagPosition &&
                    before.RuneItems.Count(row => row.Position == LegacyRuneBagPosition) >= 100) throw InvalidSpellMutation();
                if (selected.Position != LegacyRuneBagPosition)
                    items = ReplaceRuneItem(before, selected, selected.WithPosition(LegacyRuneBagPosition));
                break;
            case LegacyRuneContainerKind.Drop:
                if (selected.Locked != 0 || selected.DepositedItems.Count != 0) throw InvalidSpellMutation();
                items = RemoveLegacyRuneItem(before, selected);
                break;
            default:
                throw InvalidSpellMutation();
        }

        var grant = ResolveRecordedRuneSpell(metadata[selected.ItemId], semantics);
        if (!ReferenceEquals(items, before) && IsLegacyRuneEquipSlot(selected.ItemId, selected.Position) &&
            grant is not null && !RuneProviders(items, metadata, semantics).Contains(grant.Id) && origins.Remove(grant.Id))
        {
            rows.RemoveAll(row => row.Id == grant.Id);
            output.Add(new(LegacyRuneOperationEventKind.RemoveSpell, 0, grant.Id));
        }
        var result = LegacyRuneSpellOrigins.WithOrigins(character, origins);
        var logicalTransform = operation.Transform?.ApplyTo(character) ?? character;
        // Current Drop only removes the Rune and publishes its item/rank result.
        // It has no HP update; retain explicit old13 file-replay semantics.
        if ((semantics == LegacyRuneMappingSemantics.LegacyDisplay || operation.Kind != LegacyRuneContainerKind.Drop) &&
            LegacyTransformState.TryGetActiveMaxHitpoints(logicalTransform, out var cap))
            result = result with { MinHitPoints = Math.Min(CheckedUInt32(character.MinHitPoints, nameof(character.MinHitPoints)), cap) };
        if (result.MinHitPoints != character.MinHitPoints)
            result = result with { Attributes = new Dictionary<string, string>(result.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["MinHitPoints"] = result.MinHitPoints.ToString(CultureInfo.InvariantCulture) } };
        return new(result, items, new(rows.ToArray()), output.ToArray());
    }
}
