using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class StaminaInputs
    {
        private readonly WorldSession source;
        private readonly FullAccountStringMap attributes, paid;
        private readonly Dictionary<string, string>.Enumerator attributeWitness, paidWitness;
        private readonly LegacyPerformerSpellSource spells;
        private readonly LegacyItemSaveGraph items;
        internal WorldSession Stable { get; }

        internal StaminaInputs(WorldSession source)
        {
            this.source = source;
            attributes = new(source.Character.Attributes); paid = new(source.Character.PaidNobility);
            spells = new(source.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out var captured) || captured is null)
                throw new InvalidOperationException("staminaCurrentItemsUnproven");
            items = captured;
            Stable = items.WithOwnedItemsForPersistence(source with
            {
                Character = source.Character with { Attributes = attributes.Copy, PaidNobility = paid.Copy },
                PlayerSpells = new(new LegacyPerformerSpellRows(spells.Rows)),
            });
            attributeWitness = attributes.Open(); paidWitness = paid.Open();
        }

        internal bool IsCurrentPlain()
        {
            try { return attributes.Matches(attributeWitness) && paid.Matches(paidWitness) &&
                spells.IsCurrent() && items.IsCurrent(source.PlayerItems); }
            catch (InvalidOperationException) { return false; }
        }
    }
}
