using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record AttributeChangeData(WorldSessionStore Store, LegacyOrdinarySpellBinding Binding,
        WorldSession Expected, WorldSession Proposed);
    private static readonly ConditionalWeakTable<OwnedAttributeChange, AttributeChangeData> AttributeChanges = new();

    // Unregistered instances have no authority. Only the actual Store path below
    // registers a token after computing the packet's bounded projection.
    internal sealed class OwnedAttributeChange
    {
        internal bool IsCurrentPlain() => AttributeChanges.TryGetValue(this, out var data) &&
            data.Store.IsBoundSpellOwnerCurrent(data.Binding) &&
            data.Store.TryGet(data.Binding.Connection, out var current) && ReferenceEquals(current, data.Expected);
        internal bool Covers(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed) =>
            AttributeChanges.TryGetValue(this, out var data) && ReferenceEquals(data.Binding, binding) &&
            ReferenceEquals(data.Expected, expected) && IsCurrentPlain() &&
            SameAttributeProposal(data.Proposed.Character, proposed.Character);
    }

    private static bool SameAttributeProposal(CharacterSnapshot first, CharacterSnapshot second)
    {
        var a = LegacyNativeAttributeMapProof.CaptureRaw(first.Attributes);
        var b = LegacyNativeAttributeMapProof.CaptureRaw(second.Attributes);
        return (first with { Attributes = second.Attributes }) == second &&
            ReferenceEquals(a.CapturedComparer, b.CapturedComparer) && a.Fields.SequenceEqual(b.Fields) &&
            a.IsCurrentPlain() && b.IsCurrentPlain();
    }

    internal bool TryCommitAttributeSet(WorldPacketHandlerContext context, WorldSession expected,
        LegacyOwnedAttributeSetProposal proposal, out ActionPublication? receipt,
        out WorldPacketHandlers.PreparedAttributeRebuild? rebuild, out byte[][] frames)
    {
        receipt = null; rebuild = null; frames = [];
        RefuseBoundSpellReentry(context.Connection);
        if (framePersistenceOwner is not { } frame ||
            !frame.TryPrepareActionContext(this, context, expected, out var actual) ||
            !ReferenceEquals(expected.Character, proposal.Before) || !proposal.IsCurrentPlain()) return false;
        context = actual;
        var character = proposal.After;
        var proposed = expected with { Character = character };
        WorldSession final = null!; object identity = null!; LegacyOrdinarySpellBinding binding = null!; ActionPublication prepared = null!;
        WorldPacketHandlers.PreparedAttributeRebuild preparedRebuild; byte[][] preparedFrames;
        lock (frame.RequireActionLifecycleGate(this))
        {
            using var package = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected, proposed);
            if (!_connectionGates.TryGetValue(context.Connection, out var gate)) return false;
            lock (gate)
            {
                using var native = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(context.Connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    holder.SpellBinding is not { } owner || !BindingIsCurrentPlain(owner, holder, expected) ||
                    !frame.IsActionLifecycleCurrent(this, context) || !proposal.IsCurrentPlain() || !owner.Recovery.IsReadyForUsers(owner.UsersRoot) ||
                    !owner.Recovery.TryRequireLiveJournalClear(out _) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, proposed)) return false;
                using var actors = new SpellActorScope(owner);
                if (!WorldPacketHandlers.PreparedAttributeRebuild.TryPrepare(context, owner, expected, proposed, out var query) || query is null)
                    return false;
                preparedRebuild = query;
                preparedFrames = [proposal.Packet.ToFrameBytes()];
                var token = new OwnedAttributeChange();
                AttributeChanges.Add(token, new(this, owner, expected, proposed));
                using var publication = new PreparedSessionSpellPublication(this, context.Connection, holder, expected, proposed,
                    attributeChange: token);
                if (publication.HitpointsWrite is not null) throw InvalidSpellPublication();
                string path = Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                // IsCurrentPlain may read the prepared spell image. Acquire the
                // complete actual participant set in coordinator-sorted order.
                string[] paths = preparedRebuild.LockedSpellPath is { } spellPath ? [path, spellPath] : [path];
                bool wrote = SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedAttributeSet", out var before, out _)) return false;
                string text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
                if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before) || !AttributeCharacterFieldsUnique(text)) return false;
                var disk = CharacterSnapshotParser.ParseText(text);
                var comparableAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in expected.Character.Attributes) comparableAttributes[pair.Key] = pair.Value;
                if (disk.Uid != expected.Character.Uid || !owner.MatchesSavedVigorSeed(before) ||
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, expected.Character) ||
                    !owner.MatchesAttributeAllocationVigor(disk, expected) ||
                    disk.Attributes.Any(pair => !pair.Key.Equals("Vigor", StringComparison.OrdinalIgnoreCase) &&
                        (!comparableAttributes.TryGetValue(pair.Key, out var value) || value != pair.Value)) ||
                    disk.PaidNobility.Count != expected.Character.PaidNobility.Count ||
                    disk.PaidNobility.Any(pair => !expected.Character.PaidNobility.TryGetValue(pair.Key, out var value) || value != pair.Value)) return false;
                var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["Strength"] = proposal.Values.Strength.ToString(CultureInfo.InvariantCulture), ["Agility"] = proposal.Values.Agility.ToString(CultureInfo.InvariantCulture),
                    ["Vitaliti"] = proposal.Values.Vitality.ToString(CultureInfo.InvariantCulture), ["Spirit"] = proposal.Values.Spirit.ToString(CultureInfo.InvariantCulture),
                    ["Atributes"] = proposal.Values.Remaining.ToString(CultureInfo.InvariantCulture) };
                string changed = ProjectAttributeCharacterText(text, changes);
                var parsed = CharacterSnapshotParser.ParseText(changed);
                if (parsed.Uid != disk.Uid || CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, character)) return false;
                byte[] after = encoding.GetPreamble().Concat(encoding.GetBytes(changed)).ToArray();
                final = publication.FinalSession; identity = publication.PublishedIdentity; binding = owner;
                prepared = new(this, context, frame, identity, binding, final, LegacyActionState.RequireCurrent(final.Character));
                preparedRebuild.BindCommitted(final);
                publication.ArmFinalProof();
                if (!publication.ConsumeBeforeWriterPlain() || !preparedRebuild.IsCurrentPlain() || !proposal.IsCurrentPlain() ||
                    !frame.IsActionLifecycleCurrent(this, context)) return false;
                bool saved; bool uncertain; string reason;
                try { saved = CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(path, before, after, out uncertain, out reason); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                    NotSupportedException or System.Security.SecurityException)
                {
                    reason = error.GetType().Name; saved = false; uncertain = true;
                    try
                    {
                        if (CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedAttributeSetOutcome", out var observed, out _))
                        {
                            saved = observed.AsSpan().SequenceEqual(after);
                            uncertain = !saved && !observed.AsSpan().SequenceEqual(before);
                        }
                    }
                    catch (Exception observation) when (observation is IOException or UnauthorizedAccessException or ArgumentException or
                        NotSupportedException or System.Security.SecurityException) { }
                }
                if (!saved)
                {
                    if (!uncertain) return false;
                    FenceAttributeOutcome(holder, owner, new InvalidOperationException("ownedAttributeSetRecoveryRequired:" + reason));
                    throw new InvalidOperationException("ownedAttributeSetRecoveryRequired"); // Fence always throws.
                }
                try
                {
                    if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "ownedAttributeSetCommitted", out var observedAfter, out _) ||
                        !observedAfter.AsSpan().SequenceEqual(after) || !publication.IsCurrentPlain() ||
                        !preparedRebuild.IsCurrentPlain() || !frame.IsActionLifecycleCurrent(this, context))
                        throw InvalidSpellPublication();
                    publication.PublishNative(); publication.PublishHolder();
                    prepared.ConfirmCommitted();
                }
                catch (Exception error)
                {
                    FenceAttributeOutcome(holder, owner, error);
                    throw; // Fence always throws.
                }
                return true;
                });
                if (!wrote) return false;
            }
        }
        try { NotifyPublishedSessionChange(context.Connection, expected, final, identity, binding); }
        catch (Exception error)
        {
            prepared.FenceCommittedAttributeFailure(error);
            throw; // Fence preserves the primary and always throws.
        }
        receipt = prepared; rebuild = preparedRebuild; frames = preparedFrames;
        return true;
    }

    private static void FenceAttributeOutcome(SessionHolder holder, LegacyOrdinarySpellBinding binding, Exception primary)
    {
        var errors = new List<Exception> { primary };
        try { binding.Recovery.Block("ownedAttributeSetRecoveryRequired:outcomeUnproven"); }
        catch (Exception error) { errors.Add(error); }
        try { holder.Clear(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        throw new AggregateException("Attribute allocation outcome and fencing failed.", errors);
    }

    private static bool AttributeCharacterFieldsUnique(string text)
    {
        var sections = 0; var active = false; var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] owned = ["Strength", "Agility", "Vitaliti", "Spirit", "Atributes"];
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                active = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (active && ++sections != 1) return false;
                continue;
            }
            if (!active || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            int equals = raw.IndexOf('=');
            if (equals <= 0) return false;
            var key = raw[..equals].Trim();
            if (key.Length == 0) return false;
            if (owned.Contains(key, StringComparer.OrdinalIgnoreCase) && !keys.Add(key)) return false;
        }
        return sections == 1 && keys.Count == owned.Length;
    }

    private static string ProjectAttributeCharacterText(string text, IReadOnlyDictionary<string, string> values)
    {
        var result = new System.Text.StringBuilder(text.Length); bool active = false;
        for (int position = 0; position < text.Length;)
        {
            int end = position;
            while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
            int next = end;
            if (next < text.Length && text[next++] == '\r' && next < text.Length && text[next] == '\n') next++;
            string line = text[position..end]; string trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                active = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
            else if (active && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                int equals = line.IndexOf('=');
                if (equals > 0 && values.TryGetValue(line[..equals].Trim(), out var value))
                {
                    string old = line[(equals + 1)..];
                    int leading = old.Length - old.TrimStart().Length, trailing = old.Length - old.TrimEnd().Length;
                    if (leading + trailing > old.Length) trailing = 0;
                    line = line[..(equals + 1)] + old[..leading] + value + (trailing == 0 ? "" : old[^trailing..]);
                }
            }
            result.Append(line).Append(text.AsSpan(end, next - end)); position = next;
        }
        return result.ToString();
    }
}
