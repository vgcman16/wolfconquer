namespace WolfConquer.Core.World;

internal static partial class LegacyOrdinaryAdmissionMutationFence
{
    private static readonly object Gate = new();
    // Preserve the original fence's comparer, including on non-Windows hosts.
    private static readonly Dictionary<string, OwnerRegistration> Owners = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Participant[]> Participants = new(StringComparer.OrdinalIgnoreCase);
    private static long nextOrder;

    private sealed record Participant(LegacyOrdinaryAdmissionUidOwner Owner,
        LegacyTradeJournalFileKind Kind, string Directory, long Order);
    private sealed record OwnerRegistration(LegacyOrdinaryAdmissionUidOwner Owner, long Order, Participant[] Members);

    internal static void Register(LegacyOrdinaryAdmissionUidOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        // Roots is a sealed positional record of immutable strings; the actual
        // owner exposes one constructor-assigned, get-only Roots reference.
        // A `with` creates another record, never mutates this owner's routes.
        var roots = owner.Roots;
        var routes = roots.Kinds.Select(kind => (Kind: kind, Directory: roots.Root(kind)!)).ToArray();
        lock (Gate)
        {
            Owners.TryGetValue(roots.Users, out var previous);
            if (ReferenceEquals(previous?.Owner, owner)) return;
            var order = previous?.Order ?? nextOrder;
            var members = routes.Select(route => new Participant(owner, route.Kind, route.Directory, order)).ToArray();
            var registration = new OwnerRegistration(owner, order, members);
            var directories = members.Select(member => member.Directory)
                .Concat(previous?.Members.Select(member => member.Directory) ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var replacements = directories.Select(directory =>
            {
                Participants.TryGetValue(directory, out var old);
                var next = (old ?? []).Where(member => !ReferenceEquals(member.Owner, previous?.Owner))
                    .Concat(members.Where(member => StringComparer.OrdinalIgnoreCase.Equals(member.Directory, directory)))
                    .OrderBy(member => member.Order).ThenBy(member => member.Kind).ToArray();
                return (Directory: directory, Members: next);
            }).ToArray();
            // Prepare allocations before changing any membership. Readers see
            // the registry and all affected buckets together under this Gate.
            Owners.EnsureCapacity(Owners.Count + (previous is null ? 1 : 0));
            Participants.EnsureCapacity(Participants.Count + directories.Length);
            Owners[roots.Users] = registration;
            foreach (var replacement in replacements)
                if (replacement.Members.Length == 0) Participants.Remove(replacement.Directory);
                else Participants[replacement.Directory] = replacement.Members;
            if (previous is null) nextOrder++;
        }
    }

    // Immutable dispatch snapshots only. Retain disposed/blocked owners and
    // always evaluate their real debt and recovery authority after this lookup.
    private static Participant[] SnapshotParticipants(string? directory)
    {
        lock (Gate)
            return directory is not null && Participants.TryGetValue(directory, out var members) ? members : [];
    }

    private static (LegacyOrdinaryAdmissionUidOwner? First, LegacyOrdinaryAdmissionUidOwner? Second)
        SnapshotSessionOwners(string? previousUsers, string? nextUsers)
    {
        lock (Gate)
        {
            OwnerRegistration? previous = null, next = null;
            if (previousUsers is not null) Owners.TryGetValue(previousUsers, out previous);
            if (nextUsers is not null) Owners.TryGetValue(nextUsers, out next);
            if (ReferenceEquals(previous, next)) return (previous?.Owner, null);
            if (previous is null) return (next?.Owner, null);
            if (next is null) return (previous.Owner, null);
            return previous.Order < next.Order ? (previous.Owner, next.Owner) : (next.Owner, previous.Owner);
        }
    }

    private static string ParticipantExtension(LegacyTradeJournalFileKind kind) =>
        kind is LegacyTradeJournalFileKind.Character or LegacyTradeJournalFileKind.CombatGear ? ".ini" : ".bin";
}
