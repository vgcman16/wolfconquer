namespace WolfConquer.Core.World;

internal sealed partial class LegacyRuneOperationEvidence
{
    private sealed class BeastWitness
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativeBeastsRuntime beast;
        private readonly Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> table;
        private readonly KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>[] entries;
        private readonly (byte Level, uint RequiredPoints)[] validatedRows;
        private readonly LegacyNativeBeastsAtrribute.Attribute? row;
        private readonly bool activated;
        private readonly uint points;
        private readonly byte fixedLevel;
        internal byte Level { get; }
        internal uint Flags { get; }
        internal byte? Slots { get; }
        internal BeastWitness(LegacyOrdinarySpellBinding binding)
        {
            this.binding = binding; beast = binding.AdmittedBeasts;
            if (!beast.IsPublishedRuneOwner(binding)) throw Invalid();
            activated = beast.Activated; points = beast.Points; fixedLevel = beast.FixedLevel;
            Level = beast.Level; Flags = beast.flag;
            table = LegacyNativeBeastsAtrribute.Attributes ?? throw Invalid();
            if (table.GetType() != typeof(Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute>) ||
                !ReferenceEquals(table.Comparer, EqualityComparer<byte>.Default)) throw Invalid();
            entries = table.ToArray();
            if (entries.Length is < 1 or > byte.MaxValue) throw Invalid();
            for (var index = 0; index < entries.Length; index++)
                if (entries[index].Key != index + 1 || entries[index].Value is not { } value ||
                    value.Level != entries[index].Key || value.RequiredPoints == 0) throw Invalid();
            validatedRows = entries.Select(entry => (entry.Value.Level, entry.Value.RequiredPoints)).ToArray();
            if (table.TryGetValue(Level, out row))
            {
                if (row is null || row.Level != Level) throw Invalid();
                Slots = row.YellowRuneSlots;
            }
            else if (Level != 0) throw Invalid();
            if (!IsCurrent()) throw Invalid();
        }
        internal bool IsCurrent()
        {
            if (!beast.IsPublishedRuneOwner(binding) || beast.Activated != activated || beast.Points != points ||
                beast.FixedLevel != fixedLevel || beast.Level != Level || beast.flag != Flags ||
                !ReferenceEquals(table, LegacyNativeBeastsAtrribute.Attributes) || table.Count != entries.Length) return false;
            int index = 0;
            foreach (var entry in table)
            {
                var old = entries[index]; var values = validatedRows[index++];
                if (entry.Key != old.Key || !ReferenceEquals(entry.Value, old.Value) ||
                    values.Level != entry.Key || values.RequiredPoints == 0 ||
                    entry.Value.Level != values.Level || entry.Value.RequiredPoints != values.RequiredPoints) return false;
            }
            return row is null ? !table.ContainsKey(Level) : row.Level == Level && row.YellowRuneSlots == Slots;
        }
    }
}

internal partial class LegacyNativeBeastsRuntime
{
    internal bool IsPublishedRuneOwner(LegacyOrdinarySpellBinding binding) =>
        ReferenceEquals(binding.AdmittedBeasts, this) && ReferenceEquals(constructorOwner, binding.Client) &&
        ReferenceEquals(Owner, binding.Client) && ReferenceEquals(binding.Client.Beasts, this);
}
