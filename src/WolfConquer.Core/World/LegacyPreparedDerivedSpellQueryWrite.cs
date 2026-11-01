using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedDerivedSpellQueryWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldServerFrameHandler frame;
    private readonly WorldSessionStore store;
    private readonly WorldPacketHandlerContext context;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldPacketHandlers.OwnedDerivedSpellQueryPreparation preparation;
    private readonly LegacyNativeMagicQueryClient.DerivedSpellQuerySnapshot query;
    private readonly LegacyOwnedPhysicalMetadataWitness metadata;
    private readonly string itemsRoot, characterPath, spellsPath, itemsPath;
    private readonly byte[] characterBefore, characterAfter, spellsAfter, itemsBefore;
    private readonly byte[]? spellsBefore;
    private readonly LegacySavedSpellImage savedBefore, savedAfter;
    private readonly LegacyTradeJournalChange[] changes;
    private readonly JournalOwner journalOwner;
    private WorldPacketHandlers.OwnedDerivedSpellQueryPreparation? committed;
    private bool committing;
    internal LegacyDerivedSpellQueryOperation Operation { get; }
    internal WorldSession Proposed { get; }
    internal LegacyNativeMagicQueryClient.DerivedSpellQuerySnapshot Query => query;
    internal bool HasCommitted => ReferenceEquals(committed, preparation);
    internal byte[] CopyResponse() => preparation.CopyFrame();

    private LegacyPreparedDerivedSpellQueryWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
        WorldPacketHandlers.OwnedDerivedSpellQueryPreparation preparation)
    {
        this.frame = frame; this.store = store; this.context = context; this.binding = binding; this.preparation = preparation;
        itemsRoot = frame.RequireDerivedSpellQueryItemsRoot(store, context, binding, preparation.Expected);
        characterPath = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        spellsPath = Path.Combine(binding.SpellsRoot, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        itemsPath = Path.Combine(itemsRoot, binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        query = LegacyNativeMagicQueryClient.DerivedSpellQuerySnapshot.Capture(binding, preparation.Expected, preparation.Epoch);
        metadata = LegacyOwnedPhysicalMetadataWitness.CaptureDerivedQuery(binding, context.WorldData, preparation.Expected, preparation.Source);
        savedBefore = binding.SavedSpells;
        (characterBefore, spellsBefore, itemsBefore) = SnapshotFileLockCoordinator.ExecuteLocked(
            new[] { characterPath, spellsPath, itemsPath }, CaptureBefore);
        var expected = preparation.Expected;
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBefore, out _));
        var ranks = preparation.Source == LegacyDerivedSpellQuerySource.Rank &&
            character.Class / 1000 is 2 or 4 or 7 or 13 or 14
            ? context.WorldData!.CombatGearRanks.Select(row => new LegacyDerivedSpellQueryRankInput(row.CharacterId, row.Fields)).ToArray()
            : Array.Empty<LegacyDerivedSpellQueryRankInput>();
        var draft = new LegacyDerivedSpellQueryOperation(checked((uint)binding.Uid), preparation.Source,
            expected.AlternateEquipmentActive, itemsBefore, ranks, null);
        var desired = draft.Derive(character, spellsBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellsBefore));
        if (desired != preparation.Desired || !BeforeOwnerIsCurrent()) throw Invalid();
        MagicType? granted = null;
        if (desired.After is { } grant)
        {
            if (context.WorldData is not { } catalog || !LegacyMagicRuntime.TryResolveSpell(catalog, desired.Id, grant.Level, out var actual))
                throw Invalid();
            metadata.RequireRuntimeSpell(actual); metadata.RequireDerivedSpell(desired.Id, grant.Level);
            granted = metadata.CaptureDerivedSpellRows().Single();
        }
        Operation = new(checked((uint)binding.Uid), preparation.Source, expected.AlternateEquipmentActive, itemsBefore, ranks, granted);
        var projected = Operation.Project(characterBefore, spellsBefore);
        if (!SameBook(projected.Reconciled.Spells, preparation.Model.Spells) ||
            !SameOrigins(projected.Reconciled.Character, preparation.Model.Character)) throw Invalid();
        characterAfter = projected.CopyCharacterAfter(); spellsAfter = projected.CopySpellsAfter();
        Proposed = expected with
        {
            Character = LegacyDerivedSpellOrigins.WithOrigins(expected.Character, LegacyDerivedSpellOrigins.Read(projected.Reconciled.Character)),
            PlayerSpells = projected.Reconciled.Spells,
        };
        savedAfter = new(spellsAfter);
        changes = new[]
        {
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, binding.AccountId, characterBefore, characterAfter),
            new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Spells, binding.Uid, spellsBefore, spellsAfter),
        };
        PrepareContinuationParticipation();
        journalOwner = JournalOwner.Prepare(this);
        if (!BeforeOwnerIsCurrent()) throw Invalid();
    }

    internal static LegacyPreparedDerivedSpellQueryWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding,
        WorldPacketHandlers.OwnedDerivedSpellQueryPreparation preparation) => new(frame, store, context, binding, preparation);

    private (byte[] Character, byte[]? Spells, byte[] Items) CaptureBefore()
    {
        if (!BeforeOwnerIsCurrent() || !binding.Recovery.TryRequireLiveJournalClear(out _)) throw Invalid();
        var character = ReadImage(characterPath) ?? throw Invalid();
        var spells = ReadImage(spellsPath); var items = ReadImage(itemsPath) ?? throw Invalid();
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _));
        var expected = preparation.Expected;
        var book = spells is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spells);
        if (!savedBefore.Matches(spells) || !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, expected.Character) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, expected.Character) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, expected.Character) || !SameBook(book, expected.PlayerSpells) ||
            !PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(items)).AsSpan()
                .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems))) throw Invalid();
        LegacyDerivedSpellOrigins.RequireUnchanged(disk, book, expected.Character, expected.PlayerSpells);
        LegacyDerivedSpellOrigins.RequireSavedRows(disk, spells);
        if (!BeforeOwnerIsCurrent()) throw Invalid();
        return (character, spells, items);
    }

    private bool BeforeOwnerIsCurrent() => preparation.IsCurrentPlain(binding) && PendingBeforeIsCurrent() &&
        store.IsDerivedSpellQueryWriteOwnerCurrent(frame, context, binding, preparation.Expected) &&
        query.IsBeforeCurrentPlain(binding, preparation.Expected) && metadata.IsCurrentPlain() &&
        ReferenceEquals(binding.SavedSpells, committed is null ? savedBefore : savedAfter);

    internal Outcome TryCommit(out string reason)
    {
        reason = "derivedSpellQuery23OwnerChanged";
        if (committing || committed is not null || !BeforeOwnerIsCurrent()) return Outcome.Rejected;
        if (!binding.Recovery.TryRequireLiveJournalClear(out reason)) return Outcome.RecoveryRequired;
        if (!ConsumePendingJournalParticipation()) return Outcome.Rejected;
        committing = true;
        try
        {
            bool success = LegacyTradeJournal.TryCommitDerivedSpellQuery(binding.UsersRoot, itemsRoot, binding.SpellsRoot,
                changes, journalOwner, frame.DerivedSpellQueryJournalInterruption, out reason);
            if (!success)
                return reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal) ||
                    LegacyTradeJournal.HasPending(binding.UsersRoot) ? Outcome.RecoveryRequired : Outcome.Rejected;
            committed = preparation;
            binding.PublishSavedSpells(savedAfter);
            return Outcome.Committed;
        }
        finally { committing = false; }
    }

    private bool ExactAfterFilesAreCurrent() => SnapshotFileLockCoordinator.ExecuteLocked(
        new[] { characterPath, spellsPath, itemsPath }, () =>
            ReferenceEquals(committed, preparation) && !LegacyTradeJournal.HasPending(binding.UsersRoot) &&
            Same(ReadImage(characterPath), characterAfter) && Same(ReadImage(spellsPath), spellsAfter) && Same(ReadImage(itemsPath), itemsBefore));

    private static byte[]? ReadImage(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path)); file.Refresh();
        if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0) throw Invalid();
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        { parent.Refresh(); if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw Invalid(); }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > LegacyTradeJournal.DerivedSpellQueryMaximumReferenceBytes) throw Invalid();
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw Invalid(); return bytes;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
    private static bool SameBook(PlayerSpellSnapshot first, PlayerSpellSnapshot second) =>
        PlayerSpellSnapshotLoader.ToLegacyBytes(first).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(second));
    private static bool SameOrigins(CharacterSnapshot first, CharacterSnapshot second)
    {
        var a = LegacyDerivedSpellOrigins.Read(first); var b = LegacyDerivedSpellOrigins.Read(second);
        return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var level) && level == pair.Value);
    }
    private static InvalidOperationException Invalid() => new("derivedSpellQuery23PreparedWriterOwnerChanged");
}
