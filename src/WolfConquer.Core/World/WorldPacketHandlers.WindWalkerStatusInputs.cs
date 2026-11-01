using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class WindWalkerStatusInputs
    {
        private readonly LegacyNativeAttributeMapProof before, paid, after;
        private readonly LegacyPerformerSpellSource spells;
        private readonly LegacyItemSaveGraph items;
        private readonly WorldSession source;
        internal CharacterSnapshot Character { get; }

        internal WindWalkerStatusInputs(WorldSession source, LegacyNativeStatusFlagsBigVector32.WindWalkerSnapshot native)
        {
            this.source = source;
            before = LegacyNativeAttributeMapProof.CaptureRaw(source.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(source.Character.PaidNobility);
            var stable = source.Character with { Attributes = new Dictionary<string, string>(before.Fields, before.CapturedComparer) };
            var proposed = LegacyWindWalkerStatusProjection.Apply(stable, native);
            after = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Attributes); spells = new(source.PlayerSpells.Spells);
            if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out var captured)) throw new InvalidOperationException("WindWalker current items are unproven.");
            items = captured;
            Character = proposed;
        }

        internal bool IsCurrentPlain()
        {
            try { return before.IsCurrentPlain() && paid.IsCurrentPlain() && after.IsCurrentPlain() &&
                spells.IsCurrent() && items.IsCurrent(source.PlayerItems); }
            catch (InvalidOperationException) { return false; }
        }
    }
}
