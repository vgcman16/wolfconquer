using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// The unbound route retains the native record layout, but rejects incomplete images
// and proves the actual physical owner/map before publishing or replacing a file.
internal static partial class LegacyNativeServerDatabasePersistence
{
    private const int MaximumNativeProficiencyRows = (16 * 1024 * 1024 - sizeof(int)) /
        PlayerProficiencySnapshotLoader.LegacyProficiencySize;

    public static void LoadClientProfs(LegacyNativeGameClientRuntime client)
    {
        RequireUnboundProficiencyFile(client, null);
        var (player, profs) = CaptureProficiencyFileOwner(client, null);
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (profs.MutationGate)
        {
            RequireUnboundProficiencyFile(client, null);
            RequireProficiencyFileOwner(client, player, profs, null);
            var location = NativeProficiencyLocation.Capture(player.UID);
            location.RequireCurrentAndSafe(player);
            var file = new LegacyNativeItemRecordFile();
            if (!file.Open(location.Path, FileMode.Open)) return;
            LoadClientProfsOpenedFileCore(file, client, location);
        }
    }

    internal static void LoadClientProfsOpenedFile(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client) =>
        LoadClientProfsOpenedFileCore(binaryFile, client, null);

    private static void LoadClientProfsOpenedFileCore(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client, NativeProficiencyLocation? location)
    {
        Exception? primary = null; bool closed = false;
        try
        {
            RequireUnboundProficiencyFile(client, null);
            var (player, profs) = CaptureProficiencyFileOwner(client, null);
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (profs.MutationGate)
            {
                RequireUnboundProficiencyFile(client, null);
                RequireProficiencyFileOwner(client, player, profs, null);
                uint uid = player.UID;
                location?.RequireCurrentAndSafe(player);
                var current = profs.SnapshotCurrentRows();
                var incoming = ReadCompleteProficiencyRows(binaryFile);
                CloseOwnedSpellFile(binaryFile, null); closed = true;
                if (player.UID != uid) throw new InvalidOperationException("Proficiency player changed during load.");
                var rows = current.Proficiencies.ToList();
                var ids = rows.Select(row => row.Id).ToHashSet();
                foreach (var row in incoming) if (ids.Add(row.Id)) rows.Add(row);
                var plan = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player, current, new(rows));
                plan.ArmFinalProof();
                RequireUnboundProficiencyFile(client, null);
                RequireProficiencyFileOwner(client, player, profs, null);
                location?.RequireCurrentAndSafe(player);
                if (!plan.ConsumeBeforeWriterPlain() || !plan.ConsumeCurrentPlain())
                    throw new InvalidOperationException("Proficiency file owner or map changed before publication.");
                plan.PublishPreparedMap();
            }
        }
        catch (Exception error) { primary = error; throw; }
        finally { if (!closed) CloseOwnedSpellFile(binaryFile, primary); }
    }

    public static void SaveClientProfs(LegacyNativeGameClientRuntime client)
    {
        RequireUnboundProficiencyFile(client, null);
        var (player, profs) = CaptureProficiencyFileOwner(client, null);
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (profs.MutationGate)
        {
            RequireUnboundProficiencyFile(client, null);
            RequireProficiencyFileOwner(client, player, profs, null);
            var location = NativeProficiencyLocation.Capture(player.UID);
            location.RequireCurrentAndSafe(player);
            uint uid = player.UID;
            var current = profs.SnapshotCurrentRows();
            var bytes = PlayerProficiencySnapshotLoader.ToLegacyBytes(current);
            var plan = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player, current, current);
            if (!Directory.Exists(location.Directory)) return;
            SnapshotFileLockCoordinator.ExecuteLocked(location.Path, () =>
            {
                plan.ArmFinalProof();
                RequireUnboundProficiencyFile(client, null);
                RequireProficiencyFileOwner(client, player, profs, null);
                location.RequireCurrentAndSafe(player);
                if (player.UID != uid || !plan.ConsumeBeforeWriterPlain() || !plan.ConsumeCurrentPlain())
                    throw new InvalidOperationException("Proficiency file owner or map changed before save.");
                SnapshotFilePersistence.WriteAllBytesAtomically(location.Path, bytes, ".native-proficiency-file-");
                return true;
            });
        }
    }

    internal static void SaveClientProfsOpenedFile(LegacyNativeItemRecordFile binaryFile, LegacyNativeGameClientRuntime client)
    {
        Exception? primary = null;
        try
        {
            RequireUnboundProficiencyFile(client, null);
            var (player, profs) = CaptureProficiencyFileOwner(client, null);
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (profs.MutationGate)
            {
                RequireUnboundProficiencyFile(client, null);
                RequireProficiencyFileOwner(client, player, profs, null);
                uint uid = player.UID;
                var current = profs.SnapshotCurrentRows();
                var image = PlayerProficiencySnapshotLoader.ToLegacyBytes(current);
                var plan = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(client, player, current, current);
                plan.ArmFinalProof();
                RequireUnboundProficiencyFile(client, null);
                RequireProficiencyFileOwner(client, player, profs, null);
                if (player.UID != uid || !plan.ConsumeBeforeWriterPlain() || !plan.ConsumeCurrentPlain())
                    throw new InvalidOperationException("Proficiency file owner or map changed before save.");
                binaryFile.WriteCompleteProficiencyImage(image);
            }
        }
        catch (Exception error) { primary = error; throw; }
        finally { CloseOwnedSpellFile(binaryFile, primary); }
    }

    private static PlayerProficiencySnapshotEntry[] ReadCompleteProficiencyRows(LegacyNativeItemRecordFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        int count = 0;
        if (!file.ReadCount(ref count) || count < 0 || count > MaximumNativeProficiencyRows ||
            file.SpellFileLength != sizeof(int) + (long)count * PlayerProficiencySnapshotLoader.LegacyProficiencySize)
            throw new InvalidDataException("Invalid or incomplete proficiency file.");
        var rows = new PlayerProficiencySnapshotEntry[count];
        var ids = new HashSet<uint>();
        for (int index = 0; index < count; index++)
        {
            var raw = default(LegacyNativeClientProficiency.DBProf);
            if (!file.ReadProf(ref raw)) throw new InvalidDataException("Incomplete proficiency record.");
            var row = raw.GetClientProf();
            if (row.ID == 0 || !ids.Add(row.ID)) throw new InvalidDataException("Invalid or duplicate proficiency ID.");
            rows[index] = new(row.ID, row.Level, row.PreviouseLevel, row.Experience, row.NeededExperience);
        }
        return rows;
    }

    // A relative configuration is resolved only at capture; any later CWD or
    // configuration change invalidates it before a file operation/publication.
    private sealed record NativeProficiencyLocation(uint Uid, string Configuration, string Root, string Directory, string Path)
    {
        internal static NativeProficiencyLocation Capture(uint uid)
        {
            string configured = LegacyNativeServerConfig.DbLocation;
            if (uid == 0 || string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException("A configured proficiency root and actual UID are required.");
            string root = System.IO.Path.GetFullPath(configured);
            string directory = System.IO.Path.Combine(root, "PlayersProfs");
            return new(uid, configured, root, directory,
                System.IO.Path.Combine(directory, uid.ToString(CultureInfo.InvariantCulture) + ".bin"));
        }

        internal void RequireCurrentAndSafe(LegacyNativePlayerRuntime player)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (player.UID != Uid || !string.Equals(LegacyNativeServerConfig.DbLocation, Configuration, StringComparison.Ordinal) ||
                !string.Equals(System.IO.Path.GetFullPath(Configuration), Root, comparison))
                throw new InvalidOperationException("The configured proficiency file owner or root changed.");
            // Existing ordinary snapshot helper checks the leaf and its parent chain.
            // Its missing-leaf result is handled below, where the same chain is
            // inspected for first-save paths without creating a placeholder file.
            if (File.Exists(Path))
            {
                if (!CharacterSnapshotFileBaseline.TryValidateSafePath(Path, "nativeProficiency", out var reason))
                    throw new IOException(reason);
            }
            for (string? current = Path; !string.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
            {
                try
                {
                    var attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                        string.Equals(current, Path, comparison) && (attributes & FileAttributes.Directory) != 0)
                        throw new IOException("Proficiency path is not an ordinary file path.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
    }
    private static (LegacyNativePlayerRuntime Player, LegacyNativeProficiencyStorage Profs)
        CaptureProficiencyFileOwner(LegacyNativeGameClientRuntime client, LegacyNativeItemRecordFile? file)
    {
        ArgumentNullException.ThrowIfNull(client);
        var player = client.Player; var profs = client.MyProfs;
        RequireProficiencyFileOwner(client, player, profs, file);
        return (player!, profs!);
    }

    private static void RequireProficiencyFileOwner(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime? player, LegacyNativeProficiencyStorage? profs, LegacyNativeItemRecordFile? file)
    {
        if (player is not null && profs is not null && player.UID != 0 &&
            ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
            ReferenceEquals(client.MyProfs, profs) && profs.IsOwnedBy(client)) return;
        var error = new InvalidOperationException("Native proficiency file I/O requires its actual physical owner.");
        if (file is not null) CloseOwnedSpellFile(file, error);
        throw error;
    }

    private static void RequireUnboundProficiencyFile(LegacyNativeGameClientRuntime client, LegacyNativeItemRecordFile? ownedFile)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(client);
            if (LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client) is not null)
                throw new InvalidOperationException("Bound native proficiency file I/O is unavailable without its configured saved-image participant.");
        }
        catch (Exception error)
        {
            if (ownedFile is not null) CloseOwnedSpellFile(ownedFile, error);
            throw;
        }
    }
}
