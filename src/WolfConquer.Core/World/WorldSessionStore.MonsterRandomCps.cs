using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal bool TryCommitMonsterRandomCps(WorldPacketHandlerContext context, WorldSession expected,
        WorldPacketHandlers.MonsterRandomCpsAward award, out WorldSession? result, out ActionPublication? receipt)
    {
        result = null; receipt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (!WorldPacketHandlers.ConsumeMonsterRandomCpsAward(award, this, context, expected, out var amount)) return false;
        return TryCommitMonsterCps(context, expected, amount, out result, out receipt);
    }

    internal bool TryCommitMonsterBossCps(WorldPacketHandlerContext context, WorldSession expected,
        WorldPacketHandlers.MonsterBossCpsAward award, out WorldSession? result, out ActionPublication? receipt)
    {
        result = null; receipt = null;
        RefuseBoundSpellReentry(context.Connection);
        if (!WorldPacketHandlers.ConsumeMonsterBossCpsAward(award, this, context, expected, out var amount)) return false;
        return TryCommitMonsterCps(context, expected, amount, out result, out receipt);
    }

    private bool TryCommitMonsterCps(WorldPacketHandlerContext context, WorldSession expected, uint amount,
        out WorldSession? result, out ActionPublication? receipt)
    {
        result = null; receipt = null;
        if (framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual) ||
            actual.TradeOperations is not { } operations || !operations.OwnsActiveExclusiveLease(actual.FrameOperationLease) ||
            !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, expected)) return false;
        context = actual;
        WorldSession final = null!; object identity = null!; LegacyOrdinarySpellBinding binding = null!; ActionPublication prepared = null!;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } owner || !BindingIsCurrentPlain(owner, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) || !owner.Recovery.IsReadyForUsers(owner.UsersRoot) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out _)) return false;
                using var actors = new SpellActorScope(owner);
                LegacyNativeAttributeMapProof proof; WorldSession proposed;
                try
                {
                    var cp = checked(expected.Character.ConquerPoints + amount);
                    if (expected.Character.ConquerPoints < 0 || cp > 2_000_000_000L) return false;
                    proof = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
                    var fields = new Dictionary<string, string>(proof.CapturedComparer);
                    foreach (var pair in proof.Fields) fields.Add(pair.Key, pair.Value);
                    var keys = fields.Keys.Where(key => key.Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (keys.Length != 1) return false;
                    fields[keys[0]] = cp.ToString(CultureInfo.InvariantCulture);
                    proposed = expected with { Character = expected.Character with { ConquerPoints = cp, Attributes = fields } };
                    if (!proof.IsCurrentPlain() || !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(expected, proposed) ||
                        !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }
                PreparedSessionSpellPublication publication;
                try { publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed); }
                catch (Exception error) when (error is InvalidOperationException or InvalidDataException or OverflowException or ArgumentException)
                { return false; }
                using var publicationLease = publication;
                if (publication.HitpointsWrite is not null) return false;
                string path = Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                bool committed = SnapshotFileLockCoordinator.ExecuteLocked([path], () =>
                {
                    byte[] before, after;
                    try
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "monsterRandomCps", out before, out _)) return false;
                        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
                        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before) || !HasSingleMonsterCpsField(text)) return false;
                        var disk = CharacterSnapshotParser.ParseText(text);
                        if (!LegacyCombatCharacterImageInputs.Matches(disk, expected.Character)) return false;
                        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            { ["ConquerPoints"] = proposed.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture) };
                        var projected = ProjectAttributeCharacterText(text, replacements);
                        var parsed = CharacterSnapshotParser.ParseText(projected);
                        if (parsed.Uid != disk.Uid || CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, proposed.Character)) return false;
                        after = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or
                        OverflowException or ArgumentException or NotSupportedException or System.Security.SecurityException) { return false; }
                    final = publication.FinalSession; identity = publication.PublishedIdentity; binding = owner;
                    prepared = new(this, context, frame, identity, owner, final, LegacyActionState.RequireCurrent(final.Character));
                    publication.ArmFinalProof();
                    if (!publication.ConsumeBeforeWriterPlain() || !proof.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, context)) return false;
                    bool saved; bool uncertain; string reason;
                    try { saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out uncertain, out reason); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
                    { saved = false; uncertain = true; reason = error.GetType().Name; }
                    if (!saved)
                    {
                        if (!uncertain) return false;
                        FenceAttributeOutcome(holder, owner, new InvalidOperationException("monsterDeathCpsOutcomeUnproven:" + reason));
                        throw new InvalidOperationException("monsterDeathCpsOutcomeUnproven");
                    }
                    try
                    {
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "monsterRandomCpsCommitted", out var savedImage, out _) ||
                            !savedImage.AsSpan().SequenceEqual(after) || !publication.IsCurrentPlain() || !proof.IsCurrentPlain() ||
                            !frame.IsActionLifecycleCurrent(this, context)) throw new InvalidOperationException("monsterDeathCpsCommittedOwnerChanged");
                        publication.PublishNative(); publication.PublishHolder(); prepared.ConfirmCommitted();
                    }
                    catch (Exception error) { FenceAttributeOutcome(holder, owner, error); throw; }
                    return true;
                });
                if (!committed) return false;
            }
        }
        try { NotifyPublishedSessionChange(context.Connection, expected, final, identity, binding); }
        catch (Exception error) { FenceMonsterDeathCps(context, binding, error); throw; }
        result = final; receipt = prepared; return true;
    }

    private static bool HasSingleMonsterCpsField(string text)
    {
        int sections = 0, count = 0; bool active = false;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            { active = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase); if (active) sections++; continue; }
            if (!active || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            int equals = line.IndexOf('=');
            if (equals > 0 && line[..equals].Trim().Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase)) count++;
        }
        return sections == 1 && count == 1;
    }

    internal void FenceMonsterDeathCps(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding binding, Exception primary)
    {
        var errors = new List<Exception> { primary };
        try { binding.Recovery.Block("monsterDeathCpsCommittedOutputUnproven"); } catch (Exception error) { errors.Add(error); }
        try
        {
            if (framePersistenceOwner is { } frame)
                lock (frame.RequireActionLifecycleGate(this))
                {
                    using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, PeekMailboxSession(context.Connection));
                    if (_connectionGates.TryGetValue(context.Connection, out var gate))
                        lock (gate)
                        {
                            using var native = EnterNativeConnectionGate();
                            using var actors = new SpellActorScope(binding);
                            if (_sessions.TryGetValue(context.Connection, out var holder) &&
                                ReferenceEquals(holder.SpellBinding, binding) && ReferenceEquals(holder.NativeClientIdentity, binding.NativeIdentity))
                                holder.Clear();
                            else binding.Retire();
                        }
                    else binding.Retire();
                }
            else binding.Retire();
        }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException("Monster CP award and recovery fencing failed.", errors);
    }
}
