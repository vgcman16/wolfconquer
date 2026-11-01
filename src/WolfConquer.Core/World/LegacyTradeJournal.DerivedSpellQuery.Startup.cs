using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedDerivedSpellQueryImages(RecoveryCapture capture, uint highWater)
    {
        ValidateRecordedDerivedSpellQueryImages(capture.Record);
        RequireDerivedSpellQueryItemsCurrent(capture.Roots, capture.Record);
        var uids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(capture.Record.DerivedSpellQuery!.ItemsBefore);
        if (uids.Any(uid => uid > highWater))
            throw new InvalidDataException("Derived spell source exceeds actual item UID custody.");
        foreach (var image in new[] { capture.Record.Changes[0].Before!, capture.Record.Changes[0].After })
            LegacyOrdinaryAdmissionValidation.ValidateCharacter(CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.Decode(image, out _)));
        RequirePhysicalDeathRecoveryPrefix(capture); // Same ordered two-target DATA check, no death permission.
        RequireCharacterSpellsAccountMapping(capture, capture.Record.DerivedSpellQuery.TargetUid);
    }

    internal sealed partial class OwnedStartupItemRecovery
    {
        private void RequireDerivedSpellQueryRecoveryInputs()
        {
            if (capture.Record.Version != 23) return;
            RequireDerivedSpellQueryItemsCurrent(capture.Roots, capture.Record);
            RequirePhysicalDeathRecoveryPrefix(capture);
            RequireCharacterSpellsAccountMapping(capture, capture.Record.DerivedSpellQuery!.TargetUid);
        }

        private void RequireDerivedSpellQueryTargetsCurrent()
        {
            var targets = GetDerivedSpellQueryTargets(capture.Roots, capture.Record);
            if (targets.Length != capture.Targets.Length || targets.Where((target, index) =>
                    !SamePath(target.Path, capture.Targets[index].Path) ||
                    !ReferenceEquals(target.Change, capture.Targets[index].Change)).Any())
                throw new InvalidDataException("Derived spell configured write targets changed.");
            RequireDerivedSpellQueryRecoveryInputs();
        }
    }
}
