using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class DragonhowlLevelExperienceData
    {
        // A scalar award needs no spell participant by itself. If that same
        // recipient is a later lethal target, capture the real original files
        // now, while the original owner gates are held. These are DATA inputs;
        // only the later actual death factory can issue its journal writer.
        private sealed record ScalarDeathInputs(string ItemsPath, byte[] Items,
            LegacyPreparedSpellFileWrite Spells);
        private ScalarDeathInputs? scalarDeathInputs;

        private void CaptureScalarDeathInputs(string usersRoot, string itemRoot, string spellRoot)
        {
            if (spellFile is not null) return;
            if (!IsCurrentPlain() ||
                !PreparedAttributeRebuild.SameAttributeSpellRoot(usersRoot, binding.UsersRoot) ||
                !PreparedAttributeRebuild.SameAttributeSpellRoot(spellRoot, binding.SpellsRoot) ||
                !PreparedAttributeRebuild.SameAttributeSpellRoot(context.PlayerItemsRoot, itemRoot) ||
                !Path.IsPathFullyQualified(itemRoot) ||
                !binding.Recovery.IsReadyForCharacterItemsSpells(usersRoot, itemRoot, spellRoot) ||
                !binding.Recovery.TryRequireLiveJournalClear(out _)) throw Invalid();
            if (scalarDeathInputs is not null) return;
            string itemPath = Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetFullPath(itemRoot)),
                binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            string spellPath = Path.Combine(binding.SpellsRoot, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var captured = SnapshotFileLockCoordinator.ExecuteLocked(new[] { characterPath, itemPath, spellPath }, () =>
            {
                if (!CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlScalarDeathCharacter", out var character, out _) ||
                    !character.AsSpan().SequenceEqual(characterImage) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "dragonhowlScalarDeathItems", out var savedItems, out _) ||
                    savedItems.Length > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes ||
                    !PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(savedItems)).AsSpan()
                        .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(live.PlayerItems))) throw Invalid();
                var savedSpells = binding.Store.PrepareSpellFileWrite(binding.Connection, live,
                    binding.Recovery, binding.UsersRoot, binding.SpellsRoot);
                return new ScalarDeathInputs(itemPath, savedItems, savedSpells);
            });
            scalarDeathInputs = captured;
            // A prior award may have projected real derived-spell changes.
            // Otherwise preserve the exact nullable original disk image.
            projectedSpellImage ??= previous?.projectedSpellImage ?? captured.Spells.CopyBeforeImage();
            if (!IsCurrentPlain()) throw Invalid();
        }

        private bool ScalarDeathInputsAreCurrent()
        {
            if (scalarDeathInputs is not { } input)
                return SnapshotFileLockCoordinator.ExecuteLocked(new[] { characterPath }, () =>
                    CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlXpScalarDataCurrent", out var image, out _) &&
                    image.AsSpan().SequenceEqual(characterImage));
            return SnapshotFileLockCoordinator.ExecuteLocked(new[] { characterPath, input.ItemsPath, input.Spells.SpellsPath }, () =>
                CharacterSnapshotFileBaseline.TryReadBytes(characterPath, "dragonhowlScalarDeathCharacterCurrent", out var character, out _) &&
                character.AsSpan().SequenceEqual(characterImage) &&
                CharacterSnapshotFileBaseline.TryReadBytes(input.ItemsPath, "dragonhowlScalarDeathItemsCurrent", out var savedItems, out _) &&
                savedItems.AsSpan().SequenceEqual(input.Items) && input.Spells.DiskImageIsCurrent());
        }
    }
}
