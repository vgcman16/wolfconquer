namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Configured game evidence only. The real registry custody independently
    // owns UID pending/state/archive validation, including its exact cleanup
    // transition. This method grants no UID, replay or arbitrary-path authority.
    internal static void RequireClearRuneDecomposeNamespace(LegacyOwnedAdmissionOptions configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        if (!configured.TryNormalize(out var actual, out var reason) || actual != configured)
            throw new InvalidDataException("Rune namespace configuration is not canonical: " + reason);
        ValidateOwnedAdmissionRoutes(actual.Roots, actual.MailboxRoot, actual.RedeemFile, actual.ClaimFile);
        if (HasPending(actual.UsersRoot)) throw new InvalidDataException("Rune namespace has retained game-journal evidence.");
        var roots = actual.Roots.Kinds.Select(kind => actual.Roots.Root(kind)!).Append(actual.MailboxRoot).ToArray();
        var canonicalJournal = Path.Combine(actual.UsersRoot, JournalDirectoryName);
        foreach (var root in roots)
        {
            EnsureSafePath(root);
            if (File.Exists(root)) throw new InvalidDataException("Rune participant directory is a file.");
            if (!Directory.Exists(root)) continue; // Configured absent optional sidecars stay absent.
            var pendingDirectories = new Stack<string>(); pendingDirectories.Push(root);
            int entries = 0;
            while (pendingDirectories.TryPop(out var directory))
            {
                EnsureSafePath(directory);
                foreach (var path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > ushort.MaxValue) throw new InvalidDataException("Rune participant evidence exceeds its bound.");
                    EnsureSafePath(path);
                    var name = Path.GetFileName(path);
                    if (JournalDomainComparer.Equals(path, canonicalJournal))
                    {
                        if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any())
                            throw new InvalidDataException("Rune namespace has retained game-journal evidence.");
                        continue;
                    }
                    if (name.Contains(".managed-trade-", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Rune namespace has foreign or incomplete game staging evidence.");
                    if (Directory.Exists(path)) pendingDirectories.Push(path);
                }
            }
        }
        // Recheck after discovery. Registry replay/retirement also revalidates
        // this closed proof at each actual write boundary under its real lease.
        if (HasPending(actual.UsersRoot)) throw new InvalidDataException("Rune game-journal evidence appeared during clearance.");
        ValidateOwnedAdmissionRoutes(actual.Roots, actual.MailboxRoot, actual.RedeemFile, actual.ClaimFile);
    }
}
