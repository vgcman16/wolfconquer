using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    private static void LoadJoinedSpells(LegacyOrdinarySpellBinding binding)
    {
        var lease = binding.EnterMutation();
        try
        {
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (binding.Proficiencies.MutationGate)
            {
                var read = lease.PrepareSpellFileWrite();
                SnapshotFileLockCoordinator.ExecuteLocked(read.SpellsPath, () =>
                {
                    PublishJoinedSpellImage(binding, lease, read);
                    return true;
                });
            }
        }
        finally { lease.Dispose(); }
    }

    private static void PublishJoinedSpellImage(LegacyOrdinarySpellBinding binding,
        WorldSessionStore.NativeSpellMutationLease lease, LegacyPreparedSpellFileWrite read)
    {
        var before = read.CopyBeforeImage();
        var saved = before is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(before);
        var rows = saved.Spells.Select(row =>
        {
            var native = new LegacyNativeMsgSpell
            {
                ID = row.Id, Level = row.Level, Experience = row.Experience,
                PreviousLevel = row.PreviousLevel,
            };
            native.SoulLevel = row.SoulLevel;
            native.UseSpellSoul = row.UseSpellSoul;
            if (native.UseSpellSoul != row.UseSpellSoul)
                throw new InvalidDataException("Loaded selected soul is not representable.");
            return native;
        }).ToArray();
        var owner = new SpellFileOwner(binding.Client, binding.Player);
        owner.RequireCurrent();
        if (!ReferenceEquals(owner.Player, binding.Player))
            throw new InvalidOperationException("Loaded spell owner differs from its ordinary binding.");
        var metadata = rows.Length == 0 ? null : new SpellFileMetadata(owner.Client, owner.Player);
        var admitted = metadata is null ? rows : metadata.Filter(rows);
        var current = binding.Snapshot;
        var output = current.Spells.ToList();
        var ids = output.Select(row => row.Id).ToHashSet();
        var intent = new LegacyNativeSpellMutationIntent.MergeLoaded(admitted);
        foreach (var row in intent.Rows)
        {
            if (!ids.Add(row.ID)) continue;
            output.Add(new(row.ID, row.Level, row.Experience, row.PreviousLevel, row.SoulLevel,
                LegacyNativeSpellStorage.PreparedSessionSpellMap.SelectedSoul(row.UseSoul)));
        }
        var spells = LegacyNativeSpellStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
            current, new PlayerSpellSnapshot(output.ToArray()));
        var profs = LegacyNativeProficiencyStorage.PrepareSessionSnapshot(binding.Client, binding.Player,
            lease.ExpectedProficiencies, lease.ExpectedProficiencies);
        owner.RequireCurrent(); metadata?.RequireCurrent();
        spells.ArmFinalProof(); profs.ArmFinalProof();
        lease.PreparePublication(spells.FinalSnapshot, profs.FinalSnapshot);
        // File I/O/readiness checks precede the final raw no-comparer proof.
        // The actual file lock remains held through all plain graph stores.
        if (!read.DiskImageIsCurrent())
            throw new InvalidOperationException("Configured spell input changed during preparation.");
        owner.RequireCurrent(); metadata?.RequireCurrent();
        if (!spells.ConsumeBeforeWriterPlain() || !profs.ConsumeBeforeWriterPlain() ||
            !spells.ConsumeCurrentPlain() || !profs.ConsumeCurrentPlain() || !lease.IsCurrentPlain())
            throw new InvalidOperationException("Loaded spell publication owner changed.");
        spells.PublishPreparedMap(); profs.PublishPreparedMap(); lease.PublishPrepared();
    }
}
