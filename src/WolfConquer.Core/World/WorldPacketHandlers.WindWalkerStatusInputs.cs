using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class WindWalkerStatusInputs
    {
        private readonly FullAccountStringMap before, paid, after;
        private readonly Dictionary<string, string>.Enumerator beforeWitness, paidWitness, afterWitness;
        private readonly LegacyPerformerSpellSource spells;
        private readonly LegacyItemSaveGraph items;
        private readonly WorldSession source;
        internal CharacterSnapshot Character { get; }

        internal WindWalkerStatusInputs(WorldSession source, LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot native)
        {
            this.source = source;
            before = new(source.Character.Attributes); paid = new(source.Character.PaidNobility);
            var stable = source.Character with { Attributes = before.Copy };
            var proposed = LegacyWindWalkerStatusProjection.Apply(stable, native);
            after = new(proposed.Attributes); spells = new(source.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out var captured)) throw new InvalidOperationException("WindWalker current items are unproven.");
            items = captured;
            Character = proposed with { Attributes = after.Copy };
            // Wrapped-map/comparer callbacks finish before the native final
            // proof. Copies of these raw BCL enumerators can be reread safely.
            beforeWitness = before.Open(); paidWitness = paid.Open(); afterWitness = after.Open();
        }

        internal bool IsCurrentPlain()
        {
            try { return before.Matches(beforeWitness) && paid.Matches(paidWitness) && after.Matches(afterWitness) &&
                spells.IsCurrent() && items.IsCurrent(source.PlayerItems); }
            catch (InvalidOperationException) { return false; }
        }
    }
}
