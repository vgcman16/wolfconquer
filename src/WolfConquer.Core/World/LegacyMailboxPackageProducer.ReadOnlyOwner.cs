using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryAdmitReadOnlyOwner(WorldSession candidate, out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packageReadOnlyOwner";
        try
        {
            if (candidate.Character.Uid is <= 0 or > uint.MaxValue || !Same(candidate.UsersRoot, UsersRoot) ||
                !IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
                !TryGetSinglePending(candidate.AccountId, out var intent, out var description, out reason) ||
                intent!.Request.CharacterUid != candidate.Character.Uid) return false;
            var paths = WorkPaths(intent.Request.AccountId, intent.Request.CharacterUid);
            var failure = reason;
            var valid = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                if (!HashesMatch(paths, description!.Before) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[0], "mailboxPackageReadOnly", out var character, out failure) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[1], "mailboxPackageReadOnly", out var items, out failure) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[3], "mailboxPackageReadOnly", out var gear, out failure) ||
                    !LegacyItemSaveGraph.TryCapture(candidate.PlayerItems, out var graph) || !graph.HasBytes(items) ||
                    !LegacyTradeJournal.TryReadCompleteMailboxGear(gear, out _) || candidate.CombatGear is null ||
                    !Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(candidate.CombatGear)).AsSpan().SequenceEqual(gear)) return false;
                var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _), candidate.Character.SourceFile);
                if ((parsed with { Attributes = candidate.Character.Attributes, PaidNobility = candidate.Character.PaidNobility,
                    PlayerQuests = candidate.Character.PlayerQuests }) != candidate.Character ||
                    parsed.Attributes.Count != candidate.Character.Attributes.Count || !parsed.Attributes.All(pair =>
                        candidate.Character.Attributes.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal)))
                    return false;
                return graph.IsCurrent(candidate.PlayerItems);
            });
            reason = valid ? string.Empty : failure.Length == 0 ? "mailboxClaimRecoveryRequired:packageReadOnlyImages" : failure;
            return valid;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageReadOnlyOwner:" + error.GetType().Name; return false; }
    }
}
