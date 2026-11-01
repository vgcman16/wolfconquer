namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private LegacyPreparedBasicPhysicalWrite? committedPhysicalDeath;

    internal bool MatchesPhysicalDeathImage(byte[]? expectedImage) => SamePhysicalDeathImage(ReadImage(SpellsPath), expectedImage);

    internal void PublishCommittedPhysicalDeath(LegacyPreparedBasicPhysicalWrite actual)
    {
        if (prepared is null || after is null || !OwnerIsCurrentPlain() ||
            !actual.IsActualCommittedSpellWriter(this) || !MatchesPhysicalDeathImage(after)) throw Unavailable();
        committedPhysicalDeath = actual;
        PublishCommittedEvidencePlain();
    }

    internal bool HasCommittedPhysicalDeathAnchor(LegacyPreparedBasicPhysicalWrite actual) => committed &&
        ReferenceEquals(committedPhysicalDeath, actual) && prepared is not null && binding is not null &&
        ReferenceEquals(binding.SavedSpells, prepared) && ReferenceEquals(actual.Binding, binding);

    private static bool SamePhysicalDeathImage(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
}
