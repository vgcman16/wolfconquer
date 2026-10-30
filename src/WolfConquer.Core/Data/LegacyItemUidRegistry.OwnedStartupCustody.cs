using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // No reservation, producer or runtime can be obtained from this type. Its
    // private factory owns an existing OS lease until recovery and rescans finish.
    // The guarantee covers cooperating owners, not arbitrary raw file writers.
    internal sealed partial class OwnedStartupCustody
    {
        private readonly RegistryPaths paths;
        private readonly FileStream lease;
        private readonly byte[] stateBytes;
        private readonly RegistryState retained;
        private readonly Dictionary<string, string> archiveDigests;
        internal LegacyOwnedAdmissionOptions Configuration { get; }
        internal uint HighWater => retained.HighWater;

        private OwnedStartupCustody(LegacyOwnedAdmissionOptions configuration, RegistryPaths paths,
            FileStream lease, byte[] stateBytes, RegistryState state, ArchiveCatalog archives)
        {
            Configuration = configuration;
            this.paths = paths;
            this.lease = lease;
            this.stateBytes = stateBytes;
            retained = state;
            archiveDigests = archives.Entries.ToDictionary(pair => pair.Key,
                pair => pair.Value.Sha256, StringComparer.Ordinal);
        }

        internal void RequireRoots(string users, string items)
        {
            if (!PathComparer.Equals(paths.Users, Path.TrimEndingDirectorySeparator(Path.GetFullPath(users))) ||
                !PathComparer.Equals(paths.Items, Path.TrimEndingDirectorySeparator(Path.GetFullPath(items))))
                throw new InvalidDataException("Owned startup journal roots differ from the held namespace.");
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            // Reading this actual handle proves it remains live; a path label,
            // unrelated process absence or another file's hash cannot replace it.
            if (lease.SafeFileHandle.IsClosed || lease.SafeFileHandle.IsInvalid || !lease.CanRead || !lease.CanWrite)
                throw new IOException("Owned startup namespace lease is closed.");
            _ = lease.Length;
            EnsureOrdinaryPath(paths.Lease);
            if (!File.Exists(paths.Lease) || !TryValidateDirectory(paths, out _))
                throw new IOException("Owned startup namespace lease or directory changed.");
            RequireNoUidTransition(paths);
            if (!ReadBounded(paths.State).AsSpan().SequenceEqual(stateBytes) ||
                Guid.ParseExact(retained.Domain, "N") != Configuration.ExpectedDomain ||
                !MatchesConfiguration(retained, paths, Configuration.RegistryOptions))
                throw new InvalidDataException("Owned startup UID domain, configuration or state changed.");
            var archives = ReadArchiveCatalog(paths, retained, null, allowNext: false);
            if (archives.Entries.Count != archiveDigests.Count || archives.Entries.Any(pair =>
                !archiveDigests.TryGetValue(pair.Key, out var digest) || digest != pair.Value.Sha256))
                throw new InvalidDataException("Owned startup UID archive membership changed.");
        }

        private static void RequireNoUidTransition(RegistryPaths paths)
        {
            EnsureOrdinaryPath(paths.Pending);
            EnsureOrdinaryPath(paths.ArchivePending);
            if (File.Exists(paths.Pending) || Directory.Exists(paths.Pending) ||
                File.Exists(paths.ArchivePending) || Directory.Exists(paths.ArchivePending))
                throw new InvalidDataException("Owned startup cannot combine a pending UID or archive transition.");
            if (!File.Exists(paths.State) || Directory.Exists(paths.State))
                throw new InvalidDataException("Owned startup requires its existing complete UID state.");
        }
    }
}
