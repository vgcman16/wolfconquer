namespace WolfConquer.Core.World;

// Closed immutable descriptions of real native spell changes. Store leases
// consume these through concrete native plans; no arbitrary callback or object bag.
internal abstract record LegacyNativeSpellMutationIntent
{
    private LegacyNativeSpellMutationIntent() { }

    internal sealed record Add(ushort Id, ushort Level, byte SoulLevel, byte PreviousLevel,
        int Experience, bool ClearExperience) : LegacyNativeSpellMutationIntent;
    internal sealed record Remove(ushort Id) : LegacyNativeSpellMutationIntent;
    internal sealed record Reborn(ushort Id) : LegacyNativeSpellMutationIntent;
    internal sealed record SetSoul(ushort Id, byte SelectedSoul) : LegacyNativeSpellMutationIntent;
    internal sealed record SetProgress(ushort Id, int Experience, ushort Level) : LegacyNativeSpellMutationIntent;

    internal sealed record Clear : LegacyNativeSpellMutationIntent
    {
        private readonly ushort[] retained;
        internal ReadOnlySpan<ushort> Retained => retained;
        internal Clear(IEnumerable<ushort> retainedIds)
        {
            ArgumentNullException.ThrowIfNull(retainedIds);
            retained = retainedIds.ToArray();
        }
    }

    internal sealed record MergeLoaded : LegacyNativeSpellMutationIntent
    {
        private readonly LoadedValues[] rows;
        internal ReadOnlySpan<LoadedValues> Rows => rows;
        internal MergeLoaded(IEnumerable<LegacyNativeMsgSpell> loadedRows)
        {
            ArgumentNullException.ThrowIfNull(loadedRows);
            rows = loadedRows.Select(CopyLoaded).ToArray();
        }
        private static LoadedValues CopyLoaded(LegacyNativeMsgSpell row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return new(row.ColdTime, row.IsSpellWithColdTime, row.Size, row.PacketType, row.PacketStamp,
                row.Experience, row.ID, row.UnKnow, row.Level, row.Type, row.UseSoul, row.Soul,
                row.HashLevelSoul, row.StampSpell, row.PreviousLevel, row.SoulLevel);
        }
    }

    internal readonly record struct LoadedValues(DateTime ColdTime, bool WithColdTime,
        ushort Size, ushort PacketType, int PacketStamp, int Experience, ushort ID, ushort Unknown,
        ushort Level, ushort Type, LegacyNativeMsgSpell.UseSpellSoulTyp UseSoul, byte Soul,
        int HashLevelSoul, DateTime Stamp, byte PreviousLevel, byte SoulLevel);

    internal sealed record NinjaRefresh : LegacyNativeSpellMutationIntent
    {
        private readonly LegacyNativeSpellStorage.NinjaSkillChoice[] choices;
        private readonly ushort[] managed;
        internal ReadOnlySpan<LegacyNativeSpellStorage.NinjaSkillChoice> Choices => choices;
        internal ReadOnlySpan<ushort> Managed => managed;
        internal NinjaRefresh(IEnumerable<LegacyNativeSpellStorage.NinjaSkillChoice> selected,
            IEnumerable<ushort> managedSkills)
        {
            ArgumentNullException.ThrowIfNull(selected);
            ArgumentNullException.ThrowIfNull(managedSkills);
            choices = selected.ToArray(); managed = managedSkills.ToArray();
        }
    }
}
