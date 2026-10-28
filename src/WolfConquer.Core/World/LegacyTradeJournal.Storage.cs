using System.Globalization;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private sealed record JournalRoots(string Users, string Items, string Journal)
    {
        internal string? Quests { get; init; }
        internal string? Mailbox { get; init; }
        internal string? CombatGear { get; init; }
        internal string? Spells { get; init; }
        internal LegacyOrdinaryAdmissionRoots? Admission { get; init; }
    }

    private sealed record JournalRecord(int Version, string TransactionId, LegacyTradeJournalChange[] Changes)
    {
        // Null is omitted so v1/v2 keep their original serialized properties/order.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public QuestJournalDomain? Domain { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public MailboxJournalDomain? MailboxDomain { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public MailboxGearJournalDomain? MailboxGearDomain { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public LegacyOrdinaryAdmissionRoots? AdmissionDomain { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public LegacyOrdinaryAdmissionInput[]? AdmissionInputs { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public long? AdmissionPreparedAt { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public CharacterSpellsJournalDomain? CharacterSpellsDomain { get; init; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public CharacterItemsSpellsJournalDomain? CharacterItemsSpellsDomain { get; init; }
    }

    private sealed record JournalTarget(string Path, LegacyTradeJournalChange Change);

    private static JournalRoots GetRoots(string usersRoot, string itemsRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot) || string.IsNullOrWhiteSpace(itemsRoot))
            throw new ArgumentException("Both snapshot roots are required.");
        var users = Path.GetFullPath(usersRoot);
        var items = Path.GetFullPath(itemsRoot);
        EnsureSafePath(users);
        EnsureSafePath(items);
        if (!Directory.Exists(users)) throw new IOException("Users root is missing.");
        if (File.Exists(items)) throw new IOException("Items root is a file.");
        var journal = Path.Combine(users, JournalDirectoryName);
        EnsureSafePath(journal);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (items.Equals(journal, comparison) || items.StartsWith(journal + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Items root overlaps the journal.");
        return new(users, items, journal);
    }

    private static JournalRecord CreateRecord(IReadOnlyList<LegacyTradeJournalChange> changes, int version)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (IsOrdinaryAdmissionVersion(version) ? changes.Count is < 1 or > 4 : changes.Count != TargetCount(version))
            throw new ArgumentException("Invalid target count.");
        foreach (var change in changes)
            if (change is null || change.After is null || change.After.Length > MaxTargetBytes ||
                change.Before?.Length > MaxTargetBytes)
                throw new ArgumentException("Invalid image size.");
        var copied = changes.Select(change => change is null
            ? throw new ArgumentException("Null change.")
            : new LegacyTradeJournalChange(change.Kind, change.FileId, change.Before?.ToArray(),
                change.After?.ToArray() ?? throw new ArgumentException("Missing after image."))).ToArray();
        ValidateChanges(copied, version);
        // The single-owner API's failpoint contract is character first, then items.
        // Do not reorder v1: its established four-target contract remains unchanged.
        if (version is 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9) copied = copied.OrderBy(change => change.Kind).ToArray();
        return new(version, Guid.NewGuid().ToString("N"), copied);
    }

    private static int TargetCount(int version) => version switch
    {
        1 => 4,
        2 => 2,
        3 => 3,
        4 => 3,
        5 => 4,
        6 or 7 => 4, // Variable1..4; admission validates its exact configured membership.
        8 => 2,
        9 => 3,
        _ => throw new ArgumentException("Unsupported journal version."),
    };

    private static void ValidateChanges(LegacyTradeJournalChange[] changes, int version)
    {
        if (IsOrdinaryAdmissionVersion(version)) { ValidateAdmissionChanges(changes); return; }
        if (version == 8) { ValidateCharacterSpellsChanges(changes); return; }
        if (version == 9) { ValidateCharacterItemsSpellsChanges(changes); return; }
        if (version == 5)
        {
            ValidateMailboxGearChanges(changes);
            return;
        }
        if (version == 4)
        {
            ValidateMailboxChanges(changes);
            return;
        }
        if (version == 3)
        {
            ValidateQuestChanges(changes);
            return;
        }
        var count = TargetCount(version);
        if (changes.Length != count || changes.Count(c => c.Kind == LegacyTradeJournalFileKind.Character) != count / 2 ||
            changes.Count(c => c.Kind == LegacyTradeJournalFileKind.Items) != count / 2)
            throw new ArgumentException("Invalid character/item target shape.");
        var identities = new HashSet<(LegacyTradeJournalFileKind, long)>();
        foreach (var change in changes)
        {
            if (change.FileId <= 0 || !Enum.IsDefined(change.Kind) || !identities.Add((change.Kind, change.FileId)) ||
                change.After is null || change.After.Length > MaxTargetBytes || change.Before?.Length > MaxTargetBytes ||
                change.Kind == LegacyTradeJournalFileKind.Character && change.Before is null)
                throw new ArgumentException("Invalid journal target.");
        }
    }

    private static JournalTarget[] GetTargets(JournalRoots roots, JournalRecord record)
    {
        ValidateChanges(record.Changes, record.Version);
        if (record.Version == 3) return GetQuestTargets(roots, record);
        if (record.Version == 4) return GetMailboxTargets(roots, record);
        if (record.Version == 5) return GetMailboxGearTargets(roots, record);
        if (IsOrdinaryAdmissionVersion(record.Version)) return GetAdmissionTargets(roots, record);
        if (record.Version == 8) return GetCharacterSpellsTargets(roots, record);
        if (record.Version == 9) return GetCharacterItemsSpellsTargets(roots, record);
        var targets = record.Changes.Select(change => new JournalTarget(Path.Combine(
            change.Kind == LegacyTradeJournalFileKind.Character ? roots.Users : roots.Items,
            change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")), change)).ToArray();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (targets.Select(t => t.Path).Distinct(comparer).Count() != record.Changes.Length)
            throw new ArgumentException("Duplicate target path.");
        foreach (var target in targets) EnsureSafePath(target.Path);
        return targets;
    }

    // Check the configured roots and every existing ancestor/leaf. Never follow a
    // directory junction or symbolic link while reading, staging, or replacing bytes.
    private static void EnsureSafePath(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse paths are not supported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }
    }

    private static byte[]? ReadTarget(string path)
    {
        EnsureSafePath(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxTargetBytes) throw new IOException("Snapshot is too large.");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool Matches(byte[]? current, byte[]? expected) => current is null
        ? expected is null : expected is not null && current.AsSpan().SequenceEqual(expected);

    private static void EnsureTargetsMatch(JournalTarget[] targets, bool allowAfter)
    {
        foreach (var target in targets)
        {
            var current = ReadTarget(target.Path);
            if (!Matches(current, target.Change.Before) && (!allowAfter || !Matches(current, target.Change.After)))
                throw new IOException("Snapshot differs from journal images.");
        }
    }

    private static void EnsureAllAfter(JournalTarget[] targets)
    {
        if (targets.Any(target => !Matches(ReadTarget(target.Path), target.Change.After)))
            throw new IOException("Snapshot replacement is not complete.");
    }

    private static string StagePath(JournalTarget target, string transactionId) =>
        target.Path + ".managed-trade-" + transactionId + ".tmp";

    private static void Stage(JournalTarget target, byte[] bytes, string transactionId) =>
        WriteFlushed(StagePath(target, transactionId), bytes);

    private static void WriteFlushed(string path, byte[] bytes)
    {
        EnsureSafePath(path);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void ReplaceStaged(JournalTarget target, string transactionId)
    {
        EnsureSafePath(target.Path);
        var staged = StagePath(target, transactionId);
        EnsureSafePath(staged);
        if (File.Exists(target.Path)) File.Replace(staged, target.Path, null, ignoreMetadataErrors: true);
        else File.Move(staged, target.Path);
    }

    private static void DeleteRecord(string pending)
    {
        EnsureSafePath(pending);
        File.Delete(pending);
    }

    private static bool TryDelete(string path)
    {
        try { EnsureSafePath(path); File.Delete(path); return true; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch (Exception ex) when (IsJournalException(ex)) { return false; }
    }

    private static bool IsJournalException(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or System.Text.Json.JsonException or FormatException or
        System.Security.SecurityException;
}
