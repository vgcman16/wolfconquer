using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal enum LegacyRuneOperationEventKind { Unequip, RemoveSpell, GrantSpell }
internal sealed record LegacyRuneOperationEvent(LegacyRuneOperationEventKind Kind, uint Uid, ushort Value,
    PlayerSpellSnapshotEntry? Spell = null);
internal sealed record LegacyRuneOperationProjection(CharacterSnapshot Character, PlayerItemSnapshot Items,
    PlayerSpellSnapshot Spells, IReadOnlyList<LegacyRuneOperationEvent> Events);

public static partial class WorldPacketHandlers
{
    internal static LegacyRuneOperationProjection ProjectRecordedRuneOperation(CharacterSnapshot character,
        PlayerItemSnapshot before, PlayerSpellSnapshot spells, LegacyRuneOperation operation,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        operation.Validate(semantics);
        if (character.Reborn < 2 || character.Level < 15 || !TryGetRuneItem(before, operation.SelectedUid, out var selected) ||
            selected.Locked != 0 || selected.StackSize > 1 || selected.DepositedItems.Count != 0) throw InvalidSpellMutation();
        var metadata = RequireRuneOperationMetadata(before, selected, operation, semantics);
        LegacyRuneSpellOrigins.RequireRows(character, spells);
        var origins = LegacyRuneSpellOrigins.Read(character);
        var items = before;
        var displaced = new List<(PlayerItemSnapshotItem Row, PlayerItemSnapshot After)>();
        if (operation.Equip)
        {
            var chosen = metadata[selected.ItemId];
            var slots = checked(5 + (operation.BeastYellowSlots ?? 0) + System.Numerics.BitOperations.PopCount(operation.BeastFlags & 31));
            var requested = operation.RequestedPosition;
            if (slots > byte.MaxValue || !IsLegacyRuneEquipSlot(selected.ItemId, requested) ||
                !(slots >= requested % 100 || requested % 100 > 20 && slots + 20 >= requested % 100) ||
                !PassesLegacyItemClassRequirement(character, chosen.RequiredClass) || !PassesLegacyItemLevelRequirement(character, chosen.RequiredLevel) ||
                before.RuneItems.Any(row => row.Locked != 0 && row.Position != LegacyRuneBagPosition &&
                    (row.ItemId == selected.ItemId || row.Position == requested))) throw InvalidSpellMutation();
            Displace(row => row.ItemId == selected.ItemId && row.Position != LegacyRuneBagPosition);
            Displace(row => row.Position == requested && row.Position != LegacyRuneBagPosition);
            if (!TryGetRuneItem(items, selected.Uid, out var owned)) throw InvalidSpellMutation();
            items = ReplaceRuneItem(items, owned, owned.WithPosition(requested));
        }
        else
        {
            if (operation.RequestedPosition != LegacyRuneStoragePosition || !HasLegacyInventorySpace(before, 1) ||
                TryReadLegacyVendorState(character, out _)) throw InvalidSpellMutation();
            items = ReplaceRuneItem(items, selected, selected.WithPosition(LegacyRuneStoragePosition));
            displaced.Add((selected, items));
        }
        var providersBefore = RuneProviders(before, metadata, semantics);
        var rows = LegacyNativeSpellStorage.FreezeSessionSnapshot(spells).Spells.ToList();
        var output = new List<LegacyRuneOperationEvent>();
        RetireLegacyPassiveRuneOrigins(before, metadata, providersBefore, origins, rows, output, semantics);
        if (origins.Keys.Any(id => !providersBefore.Contains(id))) throw InvalidSpellMutation();
        foreach (var stage in displaced)
        {
            var row = stage.Row;
            if (operation.Equip) output.Add(new(LegacyRuneOperationEventKind.Unequip, row.Uid, row.Position));
            var grant = ResolveRecordedRuneSpell(metadata[row.ItemId], semantics);
            if (!IsLegacyRuneEquipSlot(row.ItemId, row.Position) || grant is null || RuneProviders(stage.After, metadata, semantics).Contains(grant.Id)) continue;
            // Legacy/unmarked rows retain unknown/permanent provenance. The
            // real IsRuneSkill caster gate requires an actual equipped provider.
            if (!origins.Remove(grant.Id)) continue;
            rows.RemoveAll(entry => entry.Id == grant.Id);
            output.Add(new(LegacyRuneOperationEventKind.RemoveSpell, 0, grant.Id));
        }
        if (operation.Equip && ResolveRecordedRuneSpell(metadata[selected.ItemId], semantics) is { } added && rows.All(row => row.Id != added.Id))
        {
            rows.Add(added); origins.Add(added.Id, checked((byte)added.Level));
            output.Add(new(LegacyRuneOperationEventKind.GrantSpell, 0, added.Id, added));
        }
        var finalCharacter = LegacyRuneSpellOrigins.WithOrigins(character, origins);
        var logicalTransform = operation.Transform?.ApplyTo(character) ?? character;
        if (LegacyTransformState.TryGetActiveMaxHitpoints(logicalTransform, out var cap))
            finalCharacter = finalCharacter with { MinHitPoints = Math.Min(CheckedUInt32(character.MinHitPoints, nameof(character.MinHitPoints)), cap) };
        if (finalCharacter.MinHitPoints != character.MinHitPoints)
        {
            var attrs = new Dictionary<string, string>(finalCharacter.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["MinHitPoints"] = finalCharacter.MinHitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            finalCharacter = finalCharacter with { Attributes = attrs };
        }
        return new(finalCharacter, items, new(rows.ToArray()), output.ToArray());

        void Displace(Func<PlayerItemSnapshotItem, bool> predicate)
        {
            var row = items.RuneItems.FirstOrDefault(predicate);
            if (row is null) return;
            items = ReplaceRuneItem(items, row, row.WithPosition(LegacyRuneStoragePosition));
            displaced.Add((row, items));
        }
    }

    private static Dictionary<uint, LegacyRuneMetadata> RequireRuneOperationMetadata(PlayerItemSnapshot before,
        PlayerItemSnapshotItem selected, LegacyRuneOperation operation, LegacyRuneMappingSemantics semantics)
    {
        if (operation.SelectedUid == 0 || operation.Metadata is null || operation.Metadata.Length == 0 ||
            operation.BeastLevel != 0 && operation.BeastYellowSlots is null ||
            !operation.Equip && (operation.BeastLevel != 0 || operation.BeastFlags != 0 || operation.BeastYellowSlots is not null)) throw InvalidSpellMutation();
        return RequireRecordedRuneMetadata(before, selected, operation.Metadata, semantics);
    }

    private static Dictionary<uint, LegacyRuneMetadata> RequireRecordedRuneMetadata(PlayerItemSnapshot before,
        PlayerItemSnapshotItem selected, LegacyRuneMetadata[] metadata,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        LegacyRuneOperation.ValidateMetadata(metadata, semantics);
        if (before.RuneItems.Any(row => row.Position is not (LegacyRuneBagPosition or LegacyRuneStoragePosition) &&
            !IsLegacyRuneEquipSlot(row.ItemId, row.Position))) throw InvalidSpellMutation();
        var expected = before.RuneItems.Where(row => IsLegacyRuneEquipSlot(row.ItemId, row.Position) || row.Uid == selected.Uid)
            .Select(row => row.ItemId).Distinct().Order().ToArray();
        if (!metadata.Select(row => row.ItemId).SequenceEqual(expected)) throw InvalidSpellMutation();
        foreach (var row in metadata) _ = ResolveRecordedRuneSpell(row, semantics);
        return metadata.ToDictionary(row => row.ItemId);
    }

    private static HashSet<ushort> RuneProviders(PlayerItemSnapshot items, Dictionary<uint, LegacyRuneMetadata> metadata,
        LegacyRuneMappingSemantics semantics = LegacyRuneMappingSemantics.RawSelf)
    {
        var result = new HashSet<ushort>();
        foreach (var row in items.RuneItems.Where(row => IsLegacyRuneEquipSlot(row.ItemId, row.Position)))
        {
            if (!metadata.TryGetValue(row.ItemId, out var value)) throw InvalidSpellMutation();
            if (ResolveRecordedRuneSpell(value, semantics) is { } spell) result.Add(spell.Id);
        }
        return result;
    }

}
