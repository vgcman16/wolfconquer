using System.Globalization;
using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // DATA only: no filesystem access and no journal owner or record escapes.
    // Count the actual JSON shape before CreateRecord clones its images or the
    // real serializer allocates the complete envelope. Strings are encoded one
    // bounded field at a time; byte images are counted without base64 allocation.
    internal static int ValidatePhysicalDeathDataEnvelope(string usersRoot, string itemsRoot, string spellsRoot,
        LegacyPhysicalDeathOperation operation, LegacyTradeJournalChange[] changes)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(changes);
        var users = PhysicalDeathDataRoot(usersRoot);
        var items = PhysicalDeathDataRoot(itemsRoot);
        var spells = PhysicalDeathDataRoot(spellsRoot);
        var journal = Path.Combine(users, JournalDirectoryName);
        if (QuestRootsOverlap(users, items) || QuestRootsOverlap(users, spells) || QuestRootsOverlap(items, spells) ||
            QuestRootsOverlap(items, journal) || QuestRootsOverlap(spells, journal))
            throw new ArgumentException("Physical death DATA roots overlap.");
        if (changes.Length != 2 || changes.Any(row => row is null || row.After is null ||
                row.After.Length > MaxTargetBytes || row.Before?.Length > MaxTargetBytes))
            throw new ArgumentException("Invalid physical death DATA image bounds.");
        long count = "{\"Version\":22,\"TransactionId\":\"00000000000000000000000000000000\",\"Changes\":[".Length;
        for (int index = 0; index < changes.Length; index++)
        {
            var row = changes[index];
            count += (index == 0 ? 0 : 1) + "{\"Kind\":".Length + DataNumber((int)row.Kind) +
                ",\"FileId\":".Length + DataNumber(row.FileId) + ",\"Before\":".Length + DataBytes(row.Before) +
                ",\"After\":".Length + DataBytes(row.After) + 1;
            if (count > MaxJournalBytes) throw new ArgumentException("Journal is too large.");
        }
        count += "],\"CharacterItemsSpellsDomain\":{\"Users\":".Length + DataString(users) +
            ",\"Items\":".Length + DataString(items) + ",\"Spells\":".Length + DataString(spells) +
            "},\"PhysicalDeath\":".Length + PhysicalDeathDataOperationBytes(operation) + 1;
        if (count > MaxJournalBytes) throw new ArgumentException("Journal is too large.");
        operation.Validate();

        var record = CreateRecord(changes, 22) with
        {
            CharacterItemsSpellsDomain = new(users, items, spells), PhysicalDeath = operation,
        };
        // PhysicalDeathOwner deliberately remains null. These are the actual
        // recipe/image and envelope validators, not a live transaction attempt.
        ValidateRecordedPhysicalDeathImages(record);
        var actual = Serialize(record);
        if (actual.Length != count) throw new InvalidDataException("Physical death DATA envelope shape changed.");
        return actual.Length;
    }

    private static string PhysicalDeathDataRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new ArgumentException("Physical death DATA roots must be absolute.");
        var canonical = NormalizeQuestRoot(root);
        if (!string.Equals(root, canonical, StringComparison.Ordinal))
            throw new ArgumentException("Physical death DATA roots must be canonical.");
        return canonical;
    }

    private static long PhysicalDeathDataOperationBytes(LegacyPhysicalDeathOperation operation)
    {
        long count = "{\"TargetUid\":".Length + DataNumber(operation.TargetUid) + ",\"QueryAdmitted\":".Length +
            (operation.QueryAdmitted ? 4 : 5) + ",\"Alternate\":".Length + (operation.Alternate ? 4 : 5) +
            ",\"ItemsBefore\":".Length + DataByteCount(operation.PhysicalDeathDataItemBytes) + ",\"RankingRows\":[".Length;
        for (int index = 0; index < operation.RankingRows.Count; index++)
        {
            var row = operation.RankingRows[index];
            count += (index == 0 ? 0 : 1) + "{\"CharacterId\":".Length + DataNumber(row.CharacterId) +
                ",\"Fields\":".Length + DataFields(row.Fields) + 1;
            if (count > MaxJournalBytes) throw new ArgumentException("Journal is too large.");
        }
        count += "],\"DerivedSpells\":[".Length;
        for (int index = 0; index < operation.DerivedSpells.Count; index++)
        {
            var row = operation.DerivedSpells[index];
            count += (index == 0 ? 0 : 1) + "{\"Id\":".Length + DataNumber(row.Id) + ",\"After\":".Length;
            // A spell is six bounded numeric fields, never an image/metadata graph.
            count += row.After is null ? 4 : JsonSerializer.SerializeToUtf8Bytes(row.After).Length;
            count++;
        }
        count += "],\"GrantedRows\":[".Length;
        for (int index = 0; index < operation.GrantedRows.Count; index++)
        {
            var row = operation.GrantedRows[index];
            count += (index == 0 ? 0 : 1) + "{\"Id\":".Length + DataNumber(row.Id) + ",\"TypeId\":".Length +
                DataNumber(row.TypeId) + ",\"Name\":".Length + DataString(row.Name) +
                ",\"Fields\":".Length + DataFields(row.Fields) + 1;
        }
        return count + 2;
    }

    private static long DataFields(IReadOnlyList<string> fields)
    {
        long count = 2;
        for (int index = 0; index < fields.Count; index++) count += (index == 0 ? 0 : 1) + DataString(fields[index]);
        return count;
    }

    private static long DataString(string value) => 2L + JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;
    private static long DataNumber(long value) => value.ToString(CultureInfo.InvariantCulture).Length;
    private static long DataBytes(byte[]? bytes) => bytes is null ? 4 : DataByteCount(bytes.Length);
    private static long DataByteCount(int length) => 2L + 4L * ((length + 2L) / 3);
}

internal sealed partial class LegacyPhysicalDeathOperation
{
    // Size-only DATA view avoids cloning the retained item image merely to
    // reject an oversized aggregate before the real record is materialized.
    internal int PhysicalDeathDataItemBytes => itemsBefore.Length;
}
