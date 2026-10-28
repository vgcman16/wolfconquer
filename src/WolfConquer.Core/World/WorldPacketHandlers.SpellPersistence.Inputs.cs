using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacySpellMutationInputs
    {
        private readonly WorldSession source;
        private readonly FullAccountStringMap attributes, paid;
        private Dictionary<string, string>.Enumerator attributeWitness, paidWitness;
        private readonly LegacyItemSaveGraph? items;
        private bool armed, consumed;
        internal LegacyPerformerSpellSource Spells { get; }
        internal WorldSession Stable { get; }

        internal LegacySpellMutationInputs(WorldSession source, bool captureItems)
        {
            if (source.Character?.Attributes is null || source.Character.PaidNobility is null || source.PlayerSpells?.Spells is null ||
                captureItems && source.PlayerItems is null) throw InvalidSpellMutation();
            this.source = source;
            Spells = new(source.PlayerSpells.Spells);
            if (captureItems)
            {
                var remaining = CharacterSnapshotFileBaseline.MaximumBytes / PlayerItemSnapshotLoader.LegacyDbItemSize;
                IReadOnlyList<PlayerItemSnapshotItem>[] groups = [source.PlayerItems.InventoryItems, source.PlayerItems.RelicFuseItems,
                    source.PlayerItems.MythsoulBagItems, source.PlayerItems.EquipmentItems, source.PlayerItems.RuneItems,
                    source.PlayerItems.WardrobeItems, source.PlayerItems.WarehouseItems];
                foreach (var group in groups)
                {
                    var rows = FullAccountKnownRows(group, remaining); remaining -= rows.Length;
                    foreach (var row in rows)
                    {
                        if (row is null || row.DepositedItems is null) throw InvalidSpellMutation();
                        var children = FullAccountKnownRows(row.DepositedItems, remaining); remaining -= children.Length;
                        foreach (var child in children)
                            if (child is null || child.DepositedItems is null || FullAccountKnownRows(child.DepositedItems, 0).Length != 0) throw InvalidSpellMutation();
                    }
                }
                if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out items)) throw InvalidSpellMutation();
            }
            attributes = new(source.Character.Attributes); paid = new(source.Character.PaidNobility);
            Stable = source with { Character = source.Character with { Attributes = attributes.Copy, PaidNobility = paid.Copy },
                PlayerSpells = new(new LegacyPerformerSpellRows(Spells.Rows)) };
            if (items is not null) Stable = items.WithOwnedItemsForPersistence(Stable);
        }

        // Wrapped-dictionary callbacks run before Store/native map preparation.
        internal void ArmPlainProof()
        {
            if (armed) throw InvalidSpellMutation();
            attributeWitness = attributes.Open(); paidWitness = paid.Open();
            armed = true;
        }

        internal bool ConsumeCurrentPlain()
        {
            if (!armed || consumed) return false;
            consumed = true;
            return attributes.Matches(attributeWitness) && paid.Matches(paidWitness) && Spells.IsCurrent() &&
                (items is null || items.IsCurrent(source.PlayerItems));
        }
    }

    private static InvalidOperationException InvalidSpellMutation() => new("spellMutationRequiresCompleteCurrentState");
    private sealed class LegacySpellMutationRefusalException(string reason) : InvalidOperationException(reason) { }
}
