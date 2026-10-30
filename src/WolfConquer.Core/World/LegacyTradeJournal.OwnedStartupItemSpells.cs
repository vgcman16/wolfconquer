using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static void ValidateOwnedStartupItemSpellImages(RecoveryCapture capture, uint highWater)
    {
        RequireCharacterItemsSpellsOrder(capture.Record.Changes);
        var character = capture.Record.Changes[0];
        var items = capture.Record.Changes[1];
        var spells = capture.Record.Changes[2];
        if (character.FileId is <= 0 or > uint.MaxValue || character.Before is null || items.Before is null || !Matches(character.Before, character.After) ||
            ReadCharacterUid(character.Before) != items.FileId || items.FileId != spells.FileId)
            throw new InvalidDataException("Owned item/spell recovery requires complete unchanged Character identity.");
        var parsedCharacter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character.Before, out _));
        LegacyOrdinaryAdmissionValidation.ValidateCharacter(parsedCharacter);
        if (!LegacyWarehouseMoneyState.TryRead(parsedCharacter.Attributes, out _) ||
            LegacyTradeEscrowClaims.ReadJournalWallet(character.Before).Claim is not null ||
            parsedCharacter.BoundConquerPoints > int.MaxValue)
            throw new InvalidDataException("Owned item/spell Character currency or escrow state is invalid.");
        var beforeUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.Before).ToHashSet();
        var afterUids = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(items.After).ToHashSet();
        if (beforeUids.Any(uid => uid > highWater) || afterUids.Any(uid => uid > highWater) || !afterUids.IsSubsetOf(beforeUids))
            throw new InvalidDataException("Owned item/spell recovery cannot introduce unregistered physical UIDs.");
        var beforeItems = PlayerItemSnapshotLoader.Parse(items.Before);
        var afterItems = PlayerItemSnapshotLoader.Parse(items.After);
        if (!LegacyItemSaveGraph.TryCapture(beforeItems, out _) || !LegacyItemSaveGraph.TryCapture(afterItems, out var afterGraph) ||
            !WorldPacketHandlers.ItemSpellRawRecordsMatch(items.Before, PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)) ||
            !afterGraph.HasBytes(items.After))
            throw new InvalidDataException("Owned item/spell Items images are not lossless through the actual producer.");
        var beforeSpells = ReadOwnedItemSpellRows(spells.Before);
        var afterSpells = ReadOwnedItemSpellRows(spells.After);
        var sameSpells = spells.Before is null
            ? spells.After.AsSpan().SequenceEqual(new byte[sizeof(int)]) : Matches(spells.Before, spells.After);
        if (sameSpells && beforeUids.SetEquals(afterUids) &&
            WorldPacketHandlers.MatchesOwnedStartupRuneItems(parsedCharacter, beforeItems, items.After)) return;
        ValidateOwnedBookImages(items, spells, beforeItems, afterItems, beforeSpells, afterSpells, beforeUids, afterUids);
    }

    private static PlayerSpellSnapshot ReadOwnedItemSpellRows(byte[]? image)
    {
        var parsed = image is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(image);
        try { return LegacyNativeSpellStorage.FreezeSessionSnapshot(parsed); }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Owned item/spell image exceeds actual native spell invariants.", error); }
    }

    private static void ValidateOwnedBookImages(LegacyTradeJournalChange items, LegacyTradeJournalChange spells,
        PlayerItemSnapshot before, PlayerItemSnapshot after, PlayerSpellSnapshot beforeSpells, PlayerSpellSnapshot afterSpells,
        HashSet<uint> beforeUids, HashSet<uint> afterUids)
    {
        if (afterSpells.Spells.Count != beforeSpells.Spells.Count + 1)
            throw new InvalidDataException("Owned book intent must append exactly one new mapped spell; other v9 projections remain unresolved.");
        PlayerItemSnapshotItem? book = null;
        var removed = beforeUids.Except(afterUids).ToArray();
        if (removed.Length == 1)
            book = before.InventoryItems.SingleOrDefault(row => row.Uid == removed[0] && row.StackSize == 1);
        else if (removed.Length == 0)
        {
            var afterRows = after.InventoryItems.ToDictionary(row => row.Uid);
            var changed = before.InventoryItems.Where(row => afterRows.TryGetValue(row.Uid, out var current) &&
                row.StackSize != current.StackSize).ToArray();
            if (changed.Length == 1 && changed[0].StackSize > 1) book = changed[0];
        }
        if (book is null || book.Locked != 0 || book.StackSize == 0 || book.DepositedItems.Count != 0 ||
            !LegacySkillBookUse.TryCreateSpellEntry(book.ItemId, out var learned) || beforeSpells.ContainsSpell(learned.Id))
            throw new InvalidDataException("Owned book intent lacks one genuine mapped inventory unit consumption.");
        var expectedItems = before with
        {
            InventoryItems = book.StackSize == 1 ? before.InventoryItems.Where(row => row.Uid != book.Uid).ToArray() :
                before.InventoryItems.Select(row => row.Uid == book.Uid ? row.WithStackSize((ushort)(row.StackSize - 1)) : row).ToArray(),
        };
        if (!PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems).AsSpan().SequenceEqual(items.After) ||
            !ProjectOwnedBookSpells(spells.Before, beforeSpells, learned).AsSpan().SequenceEqual(spells.After))
            throw new InvalidDataException("Owned book afterimages change unrelated items, spell bytes or mapped grant fields.");
    }

    private static byte[] ProjectOwnedBookSpells(byte[]? before, PlayerSpellSnapshot beforeRows, PlayerSpellSnapshotEntry learned)
    {
        // Null remains genuine physical absence in the capture. Only the AFTER
        // image is constructed, as the real prepared writer does for a first spell.
        var oldLength = before?.Length ?? sizeof(int);
        var result = new byte[checked(oldLength + PlayerSpellSnapshotLoader.LegacySpellSize)];
        before?.CopyTo(result, 0);
        BinaryPrimitives.WriteInt32LittleEndian(result, checked(beforeRows.Spells.Count + 1));
        var added = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([learned]));
        added.AsSpan(sizeof(int)).CopyTo(result.AsSpan(oldLength));
        return result;
    }
}
