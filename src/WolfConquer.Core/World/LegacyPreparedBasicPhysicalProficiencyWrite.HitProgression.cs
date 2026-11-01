namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalProficiencyWrite
{
    internal LegacyTradeJournalChange CaptureJointChange(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual)
    {
        if (!IsPreparedFor(actual) || !Plan.HasChanges || !SavedOriginIsCurrentPlain() || after is null || path is null ||
            !store.IsBasicPhysicalPreparedProficiencyOwnerCurrent(frame, hit)) throw Invalid();
        return new(LegacyTradeJournalFileKind.Proficiencies, hit.ActorBinding.Uid, before?.ToArray(), after.ToArray());
    }
    internal void AcceptCommittedHitProgression(LegacyPreparedBasicPhysicalHitProgressionWrite actual,
        WorldSessionStore.BasicPhysicalPublication token)
    {
        if (!actual.IsPreparedFor(hit, this) || !store.IsPreparedHitProgressionCurrent(token, actual) ||
            !actual.IsCommittedProficiencyWriter(this, token)) throw Invalid();
        committed = token;
        hit.ActorBinding.PublishSavedProficiencies(preparedImage ?? throw Invalid());
    }
}
