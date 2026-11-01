using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedPhysicalDeathImages(RecoveryCapture capture, uint highWater)
    {
        ValidateRecordedPhysicalDeathImages(capture.Record);
        RequirePhysicalDeathItemsCurrent(capture.Roots, capture.Record);
        var uids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(capture.Record.PhysicalDeath!.ItemsBefore);
        if (uids.Any(uid => uid > highWater))
            throw new InvalidDataException("Physical death source exceeds actual item UID custody.");
        foreach (var image in new[] { capture.Record.Changes[0].Before!, capture.Record.Changes[0].After })
            LegacyOrdinaryAdmissionValidation.ValidateCharacter(CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.Decode(image, out _)));
        RequirePhysicalDeathRecoveryPrefix(capture);
        RequirePhysicalDeathAccountMapping(capture);
    }

    private static void RequirePhysicalDeathRecoveryPrefix(RecoveryCapture capture)
    {
        var character = capture.Targets[0]; var spells = capture.Targets[1];
        var currentCharacter = ReadTarget(character.Path); var currentSpells = ReadTarget(spells.Path);
        if ((!Matches(currentCharacter, character.Change.Before) && !Matches(currentCharacter, character.Change.After)) ||
            (!Matches(currentSpells, spells.Change.Before) && !Matches(currentSpells, spells.Change.After)))
            throw new InvalidDataException("Physical death current images differ from their captured intent.");
        // Character HP always changes. If the spell image changes as well,
        // Spells-after / Character-before cannot be a real ordered prefix.
        if (!Matches(spells.Change.Before, spells.Change.After) && Matches(currentSpells, spells.Change.After) &&
            Matches(currentCharacter, character.Change.Before))
            throw new InvalidDataException("Physical death replacements are not an ordered prefix.");
    }

    private static void RequirePhysicalDeathAccountMapping(RecoveryCapture capture)
        => RequireCharacterSpellsAccountMapping(capture, capture.Record.PhysicalDeath!.TargetUid);

    private static void RequireCharacterSpellsAccountMapping(RecoveryCapture capture, uint expected)
    {
        var accounts = new Dictionary<long, long>();
        bool found = false;
        foreach (var path in Directory.EnumerateFileSystemEntries(capture.Roots.Users))
        {
            if (!Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;
            EnsureSafePath(path);
            var name = Path.GetFileNameWithoutExtension(path);
            if (Directory.Exists(path) || !long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var account) ||
                account <= 0 || name != account.ToString(CultureInfo.InvariantCulture) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "physicalDeathAccount", out var bytes, out _))
                throw new InvalidDataException("Physical death found a malformed configured Character owner.");
            var uid = ReadCharacterUid(bytes);
            if (!accounts.TryAdd(uid, account))
                throw new InvalidDataException("Configured Character files duplicate a player UID.");
            if (uid != expected) continue;
            if (account != capture.Record.Changes[0].FileId ||
                !JournalDomainComparer.Equals(path, capture.Targets[0].Path))
                throw new InvalidDataException("Physical death account differs from its configured player UID.");
            found = true;
        }
        if (!found) throw new InvalidDataException("Physical death player lacks its actual configured Character file.");
    }

    internal sealed partial class OwnedStartupItemRecovery
    {
        private void RequirePhysicalDeathRecoveryInputs()
        {
            if (capture.Record.Version != 22) return;
            RequirePhysicalDeathItemsCurrent(capture.Roots, capture.Record);
            RequirePhysicalDeathRecoveryPrefix(capture);
            RequirePhysicalDeathAccountMapping(capture);
        }

        private void RequirePhysicalDeathTargetsCurrent()
        {
            var targets = GetPhysicalDeathTargets(capture.Roots, capture.Record);
            if (targets.Length != capture.Targets.Length || targets.Where((target, index) =>
                    !SamePath(target.Path, capture.Targets[index].Path) ||
                    !ReferenceEquals(target.Change, capture.Targets[index].Change)).Any())
                throw new InvalidDataException("Physical death configured write targets changed.");
            RequirePhysicalDeathRecoveryInputs();
        }
    }
}
