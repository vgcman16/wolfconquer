using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedHitProgressionImages(RecoveryCapture capture, uint highWater)
    {
        ValidateRecordedHitProgressionImages(capture.Record);
        RequireHitProgressionReferencesCurrent(capture.Roots, capture.Record);
        var operation = capture.Record.PhysicalHitProgression!;
        var sources = operation.Death is null ? new[] { operation.ActorItems } : new[] { operation.ActorItems, operation.Death.ItemsBefore };
        foreach (var source in sources)
            if (LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(source).Any(uid => uid > highWater))
                throw new InvalidDataException("Hit progression Items exceed actual UID custody.");
        foreach (var image in new[] { capture.Record.Changes[0].Before!, capture.Record.Changes[0].After })
            LegacyOrdinaryAdmissionValidation.ValidateCharacter(CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _)));
        RequireHitProgressionPrefix(capture.Targets);
    }

    private static void RequireHitProgressionAccountMapping(JournalRoots roots, JournalRecord record)
    {
        var operation = record.PhysicalHitProgression!;
        var uids = new HashSet<long>(); bool actorFound = false, targetFound = false;
        foreach (var path in Directory.EnumerateFileSystemEntries(roots.Users))
        {
            if (!Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;
            EnsureSafePath(path); var name = Path.GetFileNameWithoutExtension(path);
            if (Directory.Exists(path) || !uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out uint account) ||
                account == 0 || name != account.ToString(CultureInfo.InvariantCulture) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "hitProgressionAccount", out var bytes, out _))
                throw new InvalidDataException("Hit progression found a malformed configured account.");
            long uid = ReadCharacterUid(bytes);
            if (!uids.Add(uid)) throw new InvalidDataException("Configured Character files duplicate a player UID.");
            if (uid == operation.ActorUid)
            {
                if (account != operation.ActorAccountId ||
                    !LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(bytes, account, operation.ActorUid))
                    throw new InvalidDataException("Actor account/UID mapping or ordinary progression availability changed.");
                actorFound = true;
            }
            if (uid == operation.TargetUid)
            {
                if (account != operation.TargetAccountId ||
                    !LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(bytes, account, operation.TargetUid))
                    throw new InvalidDataException("Target account/UID mapping or ordinary progression availability changed.");
                targetFound = true;
            }
        }
        if (!actorFound || !targetFound) throw new InvalidDataException("Hit progression lacks both configured Character owners.");
    }

    internal sealed partial class OwnedStartupItemRecovery
    {
        private void RequireHitProgressionRecoveryInputs()
        {
            if (capture.Record.Version != 24) return;
            var configured = custody.Configuration.Roots;
            if (configured.Spells is null || configured.Proficiencies is null ||
                !SamePath(configured.Spells, capture.Roots.Spells!) || !SamePath(configured.Proficiencies, capture.Roots.Proficiencies!))
                throw new InvalidDataException("Hit progression differs from configured Spells/Proficiencies custody.");
            RequireHitProgressionTargetsCurrent(capture.Roots, capture.Record, capture.Targets);
            RequireHitProgressionPrefix(capture.Targets);
            RequireHitProgressionStageDomain(capture.Roots, capture.Record, capture.Targets);
            RequireHitProgressionBinaryRoot(capture.Roots.Spells!, LegacyTradeJournalFileKind.Spells);
            RequireHitProgressionBinaryRoot(capture.Roots.Proficiencies!, LegacyTradeJournalFileKind.Proficiencies);
        }

        private void RequireHitProgressionBinaryRoot(string root, LegacyTradeJournalFileKind kind)
        {
            var targets = capture.Targets.Where(target => target.Change.Kind == kind).ToArray();
            EnsureSafePath(root);
            if (!Directory.Exists(root))
            {
                if (targets.Any(target => target.Change.Before is not null)) throw new InvalidDataException("Missing captured binary directory.");
                return;
            }
            var owners = new HashSet<uint>();
            foreach (var path in Directory.EnumerateFileSystemEntries(root))
            {
                EnsureSafePath(path);
                if (targets.Any(target => SamePath(path, StagePath(target, capture.Record.TransactionId)))) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (Directory.Exists(path) || !Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
                    !uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out uint uid) || uid == 0 ||
                    name != uid.ToString(CultureInfo.InvariantCulture) || !owners.Add(uid) ||
                    targets.Any(target => target.Change.FileId == uid && !SamePath(path, target.Path)))
                    throw new InvalidDataException("Hit progression found foreign or aliased binary evidence.");
            }
        }
    }
}
