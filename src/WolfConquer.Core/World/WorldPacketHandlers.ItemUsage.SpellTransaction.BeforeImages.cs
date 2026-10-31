using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Issued only after the actual prepared spell writer and all locked saved
    // before-images agree with this owned invocation's stable input snapshot.
    internal sealed class ItemSpellBeforeImages
    {
        private readonly LegacyPreparedSpellFileWrite writer;
        private readonly WorldSession expected, stable;
        private readonly byte[] character, items;
        private readonly byte[]? spells;
        internal string ItemPath { get; }
        internal string CharacterPath { get; }
        internal string SpellsPath { get; }

        private ItemSpellBeforeImages(LegacyPreparedSpellFileWrite writer, WorldSession expected, WorldSession stable,
            byte[] character, byte[] items, byte[]? spells, string[] paths)
        { this.writer = writer; this.expected = expected; this.stable = stable; this.character = character; this.items = items; this.spells = spells;
            CharacterPath = paths[0]; ItemPath = paths[1]; SpellsPath = paths[2]; }

        internal static ItemSpellBeforeImages Capture(WorldPacketHandlerContext context, WorldSession expected,
            WorldSession stable, LegacyPreparedSpellFileWrite writer, string[] paths)
        {
            writer.RequireItemSpellBeforeOwnerPlain(context, expected);
            writer.RequireItemSpellBeforePaths(context, paths);
            if (!ReferenceEquals(stable.NativeGameClient, expected.NativeGameClient) || stable.AccountId != expected.AccountId ||
                !FullAccountCharacterScalarsEqual(stable.Character, expected.Character) ||
                !FullAccountStoredFieldsMatch(stable.Character.Attributes, expected.Character.Attributes) ||
                !FullAccountStoredFieldsMatch(expected.Character.Attributes, stable.Character.Attributes) ||
                !FullAccountStoredFieldsMatch(stable.Character.PaidNobility, expected.Character.PaidNobility) ||
                !FullAccountStoredFieldsMatch(expected.Character.PaidNobility, stable.Character.PaidNobility) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(stable.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems)) ||
                !stable.PlayerSpells.Spells.SequenceEqual(expected.PlayerSpells.Spells)) throw InvalidSpellMutation();
            if (paths.Length != 3 || !writer.DiskImageIsCurrent()) throw InvalidSpellMutation();
            if (!CharacterSnapshotFileBaseline.TryRead(paths[0], "itemSpell", out var baseline, out var reason) || baseline is null)
                throw new LegacySpellMutationRefusalException(reason);
            var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
                throw new LegacySpellMutationRefusalException("itemSpellCharacterEncodingUnproven");
            var disk = CharacterSnapshotParser.ParseText(text);
            if (!FullAccountCharacterScalarsEqual(disk, stable.Character) || !FullAccountStoredFieldsMatch(disk.Attributes, stable.Character.Attributes) ||
                !FullAccountStoredFieldsMatch(disk.PaidNobility, stable.Character.PaidNobility))
                throw new LegacySpellMutationRefusalException("itemSpellCharacterBaselineStale");
            if (!CharacterSnapshotFileBaseline.TryReadBytes(paths[1], "itemSpell", out var itemBefore, out reason))
                throw new LegacySpellMutationRefusalException(reason);
            LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(itemBefore);
            var modeled = PlayerItemSnapshotLoader.ToLegacyBytes(stable.PlayerItems);
            if (!PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(itemBefore)).AsSpan().SequenceEqual(modeled) ||
                !ItemSpellRawRecordsMatch(itemBefore, modeled))
                throw new LegacySpellMutationRefusalException("itemSpellItemBaselineStaleOrUnrepresentable");
            var spellBefore = writer.CopyBeforeImage();
            var saved = spellBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellBefore);
            if (!LegacyNativeSpellStorage.FreezeSessionSnapshot(saved).Spells.OrderBy(row => row.Id).SequenceEqual(
                    LegacyNativeSpellStorage.FreezeSessionSnapshot(stable.PlayerSpells).Spells.OrderBy(row => row.Id))) throw InvalidSpellMutation();
            writer.RequireItemSpellBeforeOwnerPlain(context, expected);
            return new(writer, expected, stable, baseline.Bytes, itemBefore, spellBefore, paths);
        }

        internal void RequireChanceOwner(WorldPacketHandlerContext context, WorldSession actual)
        {
            RequireOwnedInvocation(context, actual);
            LegacyRuneSpellOrigins.RequireSavedRows(actual.Character, spells);
        }

        internal void RequireRuneDecomposeOwner(WorldPacketHandlerContext context, WorldSession actual, uint uid)
        {
            RequireOwnedInvocation(context, actual);
            var selected = actual.PlayerItems.RuneItems.Single(row => row.Uid == uid);
            // These actual container positions contribute no WornObjects.
            // Preserve complete current saved/native rows, without resolving
            // the removed stored item's unused spell mapping or repairing it.
            if (selected.Position is not (211 or 212)) LegacyRuneSpellOrigins.RequireSavedRows(actual.Character, spells);
        }

        private void RequireOwnedInvocation(WorldPacketHandlerContext context, WorldSession actual)
        {
            if (!ReferenceEquals(stable, actual) || actual.NativeGameClient?.OrdinarySpellBinding is null) throw InvalidSpellMutation();
            writer.RequireItemSpellBeforeOwner(context, expected);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _));
            if (!LegacyRuneSpellOrigins.Read(saved).SequenceEqual(LegacyRuneSpellOrigins.Read(actual.Character))) throw InvalidSpellMutation();
        }

        internal byte[] CopyCharacter() => character.ToArray();
        internal byte[] CopyItems() => items.ToArray();
        internal byte[]? CopySpells() => spells?.ToArray();
        internal bool HasCommittedRuneDecompose(LegacyRuneDecomposeEvidence evidence) => writer.HasCommittedRuneDecompose(evidence);
    }
}
