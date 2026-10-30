using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// UID-only raw format admission. Never use PlayerItemSnapshot.Parse here: it
// deliberately deduplicates/drops rows and cannot prove the original UID set.
internal static partial class LegacyItemUidRawCatalog
{
    internal readonly record struct Observation(uint Maximum, int PhysicalCount, int ReferenceCount);
    internal enum Kind { PlayerItems, Mailbox, Detained }
    private sealed record Input(string Path, Kind Kind);
    private const int MaximumFiles = 65535;
    private const int MaximumPhysicalRows = 2_000_000;
    private const long MaximumTotalBytes = 1024L * 1024 * 1024;
    private static readonly StringComparer Paths = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed class Scan
    {
        private readonly LegacyTradeJournal.OwnedStartupItemRecovery? recovery;
        private string inputPath = string.Empty;
        private Kind inputKind;
        internal readonly HashSet<uint> Physical = [];
        internal readonly Dictionary<uint, (string Path, byte[] Bytes)> Escrow = [];
        internal uint Maximum;
        internal int References;

        internal Scan(LegacyTradeJournal.OwnedStartupItemRecovery? recovery = null) => this.recovery = recovery;

        internal void BeginInput(string path, Kind kind) { inputPath = path; inputKind = kind; }

        internal void AddPhysical(uint uid)
        {
            recovery?.RequirePhysicalUidInput(inputPath, inputKind, uid);
            if (uid == 0 || Physical.Count == MaximumPhysicalRows || !Physical.Add(uid))
                throw new FormatException("Zero, duplicate or oversized physical UID catalog.");
            Maximum = Math.Max(Maximum, uid);
        }

        internal void AddReference(uint uid)
        {
            if (uid == 0) return;
            References = checked(References + 1);
            Maximum = Math.Max(Maximum, uid);
        }
    }

    internal static bool TryScan(string itemsRoot, string mailboxRoot, string redeemFile, string claimFile,
        out Observation observation, out string reason) =>
        TryScanCore(itemsRoot, mailboxRoot, redeemFile, claimFile, null, out observation, out reason);

    internal static bool TryScanOwnedStartup(string itemsRoot, string mailboxRoot, string redeemFile, string claimFile,
        LegacyTradeJournal.OwnedStartupItemRecovery recovery, out Observation observation, out string reason)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return TryScanCore(itemsRoot, mailboxRoot, redeemFile, claimFile, recovery, out observation, out reason);
    }

    private static bool TryScanCore(string itemsRoot, string mailboxRoot, string redeemFile, string claimFile,
        LegacyTradeJournal.OwnedStartupItemRecovery? recovery, out Observation observation, out string reason)
    {
        observation = default;
        reason = string.Empty;
        try
        {
            recovery?.RequireScanRoots(itemsRoot, mailboxRoot, redeemFile, claimFile);
            var inputs = Discover(itemsRoot, mailboxRoot, redeemFile, claimFile, recovery);
            var scan = new Scan(recovery);
            void ReadAll()
            {
                long total = 0;
                foreach (var input in inputs)
                {
                    scan.BeginInput(input.Path, input.Kind);
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(input.Path, "itemUid", out var bytes, out var failure))
                        throw new IOException(failure);
                    if (input.Kind == Kind.PlayerItems && recovery is not null)
                        bytes = recovery.ReadOwnedStartupScanImage(input.Path, bytes);
                    total += bytes.Length;
                    if (total > MaximumTotalBytes) throw new FormatException("UID bootstrap input exceeds its bound.");
                    switch (input.Kind)
                    {
                        case Kind.PlayerItems: ReadPlayerItems(bytes, scan); break;
                        case Kind.Mailbox: ReadMailbox(bytes, scan); break;
                        case Kind.Detained: ReadDetained(bytes, input.Path, scan); break;
                    }
                }
                var after = Discover(itemsRoot, mailboxRoot, redeemFile, claimFile, recovery);
                if (inputs.Length != after.Length || inputs.Where((input, index) =>
                    input.Kind != after[index].Kind || !Paths.Equals(input.Path, after[index].Path)).Any())
                    throw new IOException("UID bootstrap file set changed.");
            }
            if (inputs.Length == 0) ReadAll();
            else SnapshotFileLockCoordinator.ExecuteLocked(inputs.Select(input => input.Path).ToArray(),
                () => { ReadAll(); return true; });
            observation = new(scan.Maximum, scan.Physical.Count, scan.References);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or FormatException or OverflowException or System.Text.Json.JsonException or System.Security.SecurityException)
        { reason = "itemUidCatalogInvalid:" + exception.GetType().Name; return false; }
    }

    private static Input[] Discover(string itemsRoot, string mailboxRoot, string redeemFile, string claimFile,
        LegacyTradeJournal.OwnedStartupItemRecovery? recovery)
    {
        recovery?.RequireCurrent();
        var result = new List<Input>();
        void DirectoryFiles(string directory, Kind kind)
        {
            EnsureOrdinary(directory);
            if (File.Exists(directory)) throw new IOException("Expected UID source directory.");
            if (!Directory.Exists(directory)) return;
            var owners = new HashSet<uint>();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                EnsureOrdinary(entry);
                if (kind == Kind.PlayerItems && Directory.Exists(entry) &&
                    Paths.Equals(Path.GetFileName(entry), ".managed-item-uids")) continue;
                if (kind == Kind.PlayerItems && recovery?.IsKnownItemStage(entry) == true) continue;
                if (Directory.Exists(entry) || !Path.GetExtension(entry).Equals(".bin", StringComparison.OrdinalIgnoreCase) ||
                    !uint.TryParse(Path.GetFileNameWithoutExtension(entry), NumberStyles.None, CultureInfo.InvariantCulture, out var owner) ||
                    owner == 0 || Path.GetFileNameWithoutExtension(entry) != owner.ToString(CultureInfo.InvariantCulture) ||
                    !owners.Add(owner))
                    throw new FormatException("Unknown UID source entry or aliased owner filename.");
                if (result.Count == MaximumFiles) throw new FormatException("Too many UID source files.");
                result.Add(new(Path.GetFullPath(entry), kind));
            }
        }
        DirectoryFiles(itemsRoot, Kind.PlayerItems);
        DirectoryFiles(mailboxRoot, Kind.Mailbox);
        foreach (var file in new[] { redeemFile, claimFile })
        {
            EnsureOrdinary(file);
            if (Directory.Exists(file)) throw new IOException("Expected detained UID source file.");
            if (!File.Exists(file)) continue;
            if (result.Count == MaximumFiles) throw new FormatException("Too many UID source files.");
            result.Add(new(Path.GetFullPath(file), Kind.Detained));
        }
        var ordered = result.OrderBy(input => input.Path, Paths).ToArray();
        if (ordered.Select(input => input.Path).Distinct(Paths).Count() != ordered.Length)
            throw new FormatException("UID source paths overlap.");
        recovery?.RequireCurrent();
        return ordered;
    }

    private static void EnsureOrdinary(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("UID catalog rejects reparse paths.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            var parent = Path.GetDirectoryName(current);
            if (parent == current) break;
            current = parent;
        }
    }
}
