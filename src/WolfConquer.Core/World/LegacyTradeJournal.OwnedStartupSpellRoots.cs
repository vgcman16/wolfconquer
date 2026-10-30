using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal sealed partial class OwnedStartupItemRecovery
    {
        private void RequireConfiguredSpellsRoot()
        {
            if (capture.Record.Version is not (9 or 12 or 13 or 14 or 15)) return;
            var configured = custody.Configuration;
            if (capture.Roots.Spells is not { } spells || !SamePath(spells, configured.SpellsRoot))
                throw new InvalidDataException("Owned item/spell recovery differs from its configured Spells root.");
            RequireCharacterItemsSpellsTargetsCurrent(capture.Roots, capture.Record, capture.Targets);
            var targets = capture.Targets.Where(target => target.Change.Kind == LegacyTradeJournalFileKind.Spells).ToArray();
            if (!Directory.Exists(spells))
            {
                if (targets.Single().Change.Before is not null)
                    throw new InvalidDataException("Missing Spells directory differs from its captured existing beforeimage.");
                return; // Actual producer can publish pending before creating its first Spells directory.
            }
            var owners = new HashSet<uint>();
            foreach (var path in Directory.EnumerateFileSystemEntries(spells))
            {
                EnsureSafePath(path);
                if (targets.Any(target => SamePath(path, StagePath(target, capture.Record.TransactionId))))
                    continue; // RequireCurrent validates this exact captured stage's bytes too.
                var name = Path.GetFileNameWithoutExtension(path);
                if (Directory.Exists(path) || !Path.GetExtension(path).Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
                    !uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var owner) || owner == 0 ||
                    name != owner.ToString(CultureInfo.InvariantCulture) || !owners.Add(owner) ||
                    targets.Any(target => target.Change.FileId == owner && !SamePath(path, target.Path)))
                    throw new InvalidDataException("Owned item/spell recovery found foreign or aliased Spells evidence.");
            }
        }

        internal void PrepareDirectories()
        {
            RequireCurrent();
            if (capture.Record.Version is 9 or 12 or 13 or 14 or 15)
                PrepareCharacterItemsSpellsDirectories(capture.Roots, capture.Record, capture.Targets);
            RequireCurrent();
        }
    }
}
