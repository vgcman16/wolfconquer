namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool IsPreparedHitProgressionCurrent(BasicPhysicalPublication publication,
        LegacyPreparedBasicPhysicalHitProgressionWrite writer) =>
        basicPhysicalPublications.TryGetValue(publication, out var actual) && actual.CommittedWriter is null &&
        ReferenceEquals(actual.JointProgressionWriter, writer) && writer.IsPreparedFor(actual.Plan, actual.ProficiencyWrite) &&
        IsBasicPhysicalPreparedProficiencyOwnerCurrent(actual.Frame, actual.Plan);
}
