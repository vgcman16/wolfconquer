using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Actual target death Character+Spells writer. The operation/history are DATA;
// only this same prepared current Frame/Store/plan issues its journal owner.
internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    internal readonly WorldServerFrameHandler Frame;
    internal readonly WorldSessionStore Store;
    internal readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation Plan;
    internal readonly LegacyOrdinarySpellBinding Binding;
    internal readonly WorldServerFrameHandler.BasicPhysicalFileRoots Roots;
    internal readonly string CharacterPath, ItemPath, SpellPath;
    private readonly LegacyPreparedSpellFileWrite spells;
    private readonly byte[] characterBefore, characterAfter, itemsBefore;
    private readonly byte[]? spellsBefore;
    private readonly byte[] spellsAfter;
    private readonly JournalOwner journalOwner;
    private LegacyPhysicalDeathOperation? committed;
    internal LegacyPhysicalDeathOperation Operation { get; }
    internal WorldSession Proposed => Plan.Target;

    private LegacyPreparedBasicPhysicalWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation plan)
    {
        Frame = frame; Store = store; Plan = plan; Binding = plan.TargetBinding;
        if (plan.Death is null || !plan.IsCurrentPlain()) throw Unavailable();
        Roots = frame.RequireBasicPhysicalFileRoots(store, Binding);
        CharacterPath = Path.Combine(Roots.Users, Binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        ItemPath = Path.Combine(Roots.Items, Binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        SpellPath = Path.Combine(Roots.Spells, Binding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var captured = SnapshotFileLockCoordinator.ExecuteLocked(Paths, () =>
        {
            if (!plan.IsCurrentPlain() || !Binding.Recovery.TryRequireLiveJournalClear(out _) ||
                !plan.Death.TryCapturePhysicalDeathHistory(Binding, out var history)) throw Unavailable();
            if (!CharacterSnapshotFileBaseline.TryReadBytes(CharacterPath, "basicPhysicalDeath", out var character, out var reason) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(ItemPath, "basicPhysicalDeathItems", out var items, out reason))
                throw new IOException("basicPhysicalDeathBeforeUnavailable:" + reason);
            var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(character, out _));
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, plan.ExpectedTarget.Character) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, plan.ExpectedTarget.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(parsed, plan.ExpectedTarget.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(items)).AsSpan().SequenceEqual(history.SourceItems))
                throw Unavailable();
            var write = store.PrepareSpellFileWrite(Binding.Connection, plan.ExpectedTarget, Binding.Recovery, Roots.Users, Roots.Spells);
            var spellBefore = write.CopyBeforeImage();
            var saved = spellBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellBefore);
            if (!PlayerSpellSnapshotLoader.ToLegacyBytes(saved).AsSpan().SequenceEqual(history.SourceSpells) ||
                !history.SourceSpells.AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(plan.ExpectedTarget.PlayerSpells)))
                throw Unavailable();
            write.PrepareImage(plan.Target.PlayerSpells);
            var operation = new LegacyPhysicalDeathOperation(checked((uint)Binding.Uid), history.QueryAdmitted, history.Alternate,
                items, history.RankingRows, history.DerivedSpells, history.GrantedRows);
            operation.Validate();
            var expected = plan.ExpectedTarget.Character with { MinHitPoints = 0 };
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(expected, plan.Target.Character) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected, plan.Target.Character) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(expected, plan.Target.Character)) throw Unavailable();
            var after = operation.ProjectCharacter(character, spellBefore);
            RequireRecordedDerivedOrigins(after, plan.Target.Character, plan.Target.PlayerSpells);
            var spellAfter = write.CopyAfterImage();
            if (!operation.ProjectSpells(spellBefore, parsed).AsSpan().SequenceEqual(spellAfter) || !plan.IsCurrentPlain())
                throw Unavailable();
            return (Write: write, CharacterBefore: character, CharacterAfter: after, Items: items,
                SpellsBefore: spellBefore, SpellsAfter: spellAfter, Operation: operation);
        });
        spells = captured.Write; characterBefore = captured.CharacterBefore; characterAfter = captured.CharacterAfter;
        itemsBefore = captured.Items; spellsBefore = captured.SpellsBefore; spellsAfter = captured.SpellsAfter;
        Operation = captured.Operation; journalOwner = new(this);
        if (!IsCurrent()) throw Unavailable();
    }

    internal static LegacyPreparedBasicPhysicalWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation plan) => new(frame, store, plan);
    private string[] Paths => [CharacterPath, ItemPath, SpellPath];
    internal bool IsCurrent() => Plan.IsCurrentPlain() && spells.OwnerIsCurrentPlain();
    internal bool IsActualCommittedSpellWriter(LegacyPreparedSpellFileWrite actual) => ReferenceEquals(spells, actual) &&
        ReferenceEquals(committed, Operation) && Plan.IsCurrentPlain() && !LegacyTradeJournal.HasPending(Roots.Users) && ImagesMatch(after: true);
    private static InvalidOperationException Unavailable() => new("basicPhysicalDeathRequiresCurrentCapturedOwner");
}
