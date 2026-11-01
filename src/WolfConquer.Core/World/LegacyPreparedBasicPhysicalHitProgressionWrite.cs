using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only the real Store's same prepared receive and concrete component writers
// can issue this owner. The historical DATA operation never issues permission.
internal sealed partial class LegacyPreparedBasicPhysicalHitProgressionWrite
{
    internal enum Outcome { Rejected, Committed, RecoveryRequired }
    private readonly WorldSessionStore store;
    private readonly WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit;
    private readonly LegacyPreparedBasicPhysicalProficiencyWrite progression;
    private readonly LegacyPreparedBasicPhysicalWrite? death;
    private readonly LegacyBoundHitpointsScalarWrite hp;
    private readonly WorldSessionStore.BasicPhysicalPublication publication;
    private readonly WorldServerFrameHandler.BasicPhysicalFileRoots roots;
    private readonly string proficiencies, actorCharacterPath, actorItemsPath;
    private readonly byte[] actorCharacter, actorItems;
    private readonly LegacyTradeJournalChange[] changes;
    private readonly string[] paths;
    private readonly JournalOwner owner;
    private bool committed;
    internal LegacyPhysicalHitProgressionOperation Operation { get; }

    private LegacyPreparedBasicPhysicalHitProgressionWrite(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit, LegacyPreparedBasicPhysicalProficiencyWrite progression,
        LegacyPreparedBasicPhysicalWrite? death, LegacyBoundHitpointsScalarWrite hp, WorldSessionStore.BasicPhysicalPublication publication)
    {
        this.store = store; this.hit = hit; this.progression = progression;
        this.death = death; this.hp = hp; this.publication = publication;
        if (!hit.IsCurrentPlain() || !OrdinaryParticipantsAreClear() || !progression.IsPreparedFor(hit) || !progression.Plan.HasChanges ||
            !hp.IsPreparedForHitProgression(hit) || (hit.Death is null) != (death is null)) throw Invalid();
        roots = frame.RequireBasicPhysicalFileRoots(store, hit.TargetBinding);
        if (roots != frame.RequireBasicPhysicalFileRoots(store, hit.ActorBinding)) throw Invalid();
        proficiencies = frame.RequireBasicPhysicalProficiencyRoot(store, hit.ActorBinding);
        if (!hit.ActorBinding.Recovery.IsReadyForHitProgression(roots.Users, roots.Items, roots.Spells, proficiencies) ||
            !hit.TargetBinding.Recovery.IsReadyForHitProgression(roots.Users, roots.Items, roots.Spells, proficiencies)) throw Invalid();
        actorCharacterPath = Path.Combine(roots.Users, hit.ActorBinding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        actorItemsPath = Path.Combine(roots.Items, hit.ActorBinding.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var character = new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, hit.TargetBinding.AccountId,
            hp.CopyBeforeImage(), hp.CopyAfterImage());
        changes = death is null ? [character, progression.CaptureJointChange(hit)] :
            [character, death.CaptureHitProgressionSpells(hit, character), progression.CaptureJointChange(hit)];
        paths = changes.Select(change => Path.Combine(change.Kind switch
        {
            LegacyTradeJournalFileKind.Character => roots.Users,
            LegacyTradeJournalFileKind.Spells => roots.Spells,
            LegacyTradeJournalFileKind.Proficiencies => proficiencies,
            _ => throw Invalid(),
        }, change.FileId.ToString(CultureInfo.InvariantCulture) +
            (change.Kind == LegacyTradeJournalFileKind.Character ? ".ini" : ".bin")))
            .Concat(new[] { actorCharacterPath, actorItemsPath })
            .Concat(death is null ? Array.Empty<string>() : new[] { death.ItemPath }).ToArray();
        (actorCharacter, actorItems) = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
        {
            if (!hit.IsCurrentPlain() || !CharacterSnapshotFileBaseline.TryReadBytes(actorCharacterPath, "hitProgressionActor", out var actor, out _) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(actorItemsPath, "hitProgressionItems", out var items, out _)) throw Invalid();
            var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(actor, out _));
            if (parsed.Uid != hit.ActorBinding.Uid ||
                !LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(actor, hit.ActorBinding.AccountId, checked((uint)hit.ActorBinding.Uid)) ||
                !LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(changes[0].Before!, hit.TargetBinding.AccountId, checked((uint)hit.TargetBinding.Uid)) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(items)).AsSpan().SequenceEqual(
                    PlayerItemSnapshotLoader.ToLegacyBytes(hit.ExpectedActor.PlayerItems))) throw Invalid();
            return (actor, items);
        });
        Operation = new(hit.ActorBinding.AccountId, checked((uint)hit.ActorBinding.Uid), hit.TargetBinding.AccountId,
            checked((uint)hit.TargetBinding.Uid), hit.ProficiencyDamage,
            checked((uint)(hit.ExpectedTarget.Character.MinHitPoints - hit.Target.Character.MinHitPoints)),
            hit.Actor.AlternateEquipmentActive, actorItems, death?.Operation);
        var projected = Operation.Project(changes[0].Before!, changes[^1].Before, death is null ? null : changes[1].Before);
        if (!projected.CopyCharacterAfter().AsSpan().SequenceEqual(changes[0].After) ||
            !projected.CopyProficienciesAfter().AsSpan().SequenceEqual(changes[^1].After) ||
            !projected.WeaponIds.SequenceEqual(progression.Plan.WeaponIds) ||
            death is not null && !Same(projected.CopySpellsAfter(), changes[1].After)) throw Invalid();
        owner = new(this);
    }

    internal static LegacyPreparedBasicPhysicalHitProgressionWrite Prepare(WorldServerFrameHandler frame, WorldSessionStore store,
        WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation hit, LegacyPreparedBasicPhysicalProficiencyWrite progression,
        LegacyPreparedBasicPhysicalWrite? death, LegacyBoundHitpointsScalarWrite hp, WorldSessionStore.BasicPhysicalPublication publication)
        => new(frame, store, hit, progression, death, hp, publication);

    internal bool IsPreparedFor(WorldPacketHandlers.OwnedBasicPhysicalReceivePreparation actual,
        LegacyPreparedBasicPhysicalProficiencyWrite proficiencies) => ReferenceEquals(hit, actual) && ReferenceEquals(progression, proficiencies);
    private bool IsCurrent() => store.IsPreparedHitProgressionCurrent(publication, this) && hit.IsCurrentPlain() &&
        OrdinaryParticipantsAreClear() && progression.SavedOriginIsCurrentPlain() &&
        hp.IsPreparedForHitProgression(hit) && (death is null || death.IsCurrent());
    private bool OrdinaryParticipantsAreClear() =>
        LegacyExperienceContinuationFence.AllowsOrdinaryPublication(hit.ExpectedActor, hit.Actor) &&
        LegacyExperienceContinuationFence.AllowsOrdinaryPublication(hit.ExpectedTarget, hit.Target);
    private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
        second is not null && first.AsSpan().SequenceEqual(second);
    private static InvalidOperationException Invalid() => new("physicalHitProgressionRequiresSameActualPreparedHit");
}
