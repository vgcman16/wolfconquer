using System.Collections.Concurrent;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    private readonly ConcurrentDictionary<long, Guid> awaitingSessionPublication = new();

    private void RetainSessionPublication(long account, Guid operation)
    {
        if (!awaitingSessionPublication.TryAdd(account, operation) &&
            (!awaitingSessionPublication.TryGetValue(account, out var current) || current != operation))
            throw new InvalidOperationException("Another package awaits session publication.");
    }

    internal bool TryFinishSessionPublication(WorldSessionStore store, ConquerTcpConnection connection, WorldSession session)
    {
        if (!ReferenceEquals(store.MailboxPackages, this) || !store.TryGet(connection, out var current) ||
            !ReferenceEquals(current, session) || !awaitingSessionPublication.TryGetValue(session.AccountId, out var operation)) return false;
        try
        {
            var row = workStore.Read(operation);
            if (row is not { Completed: true } || row.AccountId != session.AccountId || row.CharacterUid != session.Character.Uid ||
                !Same(session.UsersRoot, UsersRoot)) return false;
            if (row.Version == 1)
            {
                if (!Registry.TryReadProducerIntent(Domain, operation, out var intent, out _) ||
                    intent is null || !workStore.MatchesIntent(row, intent)) return false;
            }
            else if (!TryValidateNoAllocationRecord(row, out _) ||
                !Registry.TryFindProducerIntent(Domain, operation, out _, out var found, out _) || found) return false;
            var paths = workStore.TargetPaths(row);
            return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
            {
                if (!workStore.ImagesMatch(row, after: true) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[0], "mailboxPackagePublication", out var characterBytes, out _) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[1], "mailboxPackagePublication", out var itemsBytes, out _) ||
                    !CharacterSnapshotFileBaseline.TryReadBytes(paths[3], "mailboxPackagePublication", out var gearBytes, out _) ||
                    !LegacyItemSaveGraph.TryCapture(session.PlayerItems, out var graph) || !graph.HasBytes(itemsBytes) ||
                    session.CombatGear is null ||
                    !Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(session.CombatGear)).AsSpan().SequenceEqual(gearBytes)) return false;
                var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBytes, out _), session.Character.SourceFile);
                if (parsed.Uid != session.Character.Uid || parsed.Money != session.Character.Money ||
                    parsed.ConquerPoints != session.Character.ConquerPoints || parsed.Class != session.Character.Class ||
                    !SamePublicationAttribute(parsed.Attributes, session.Character.Attributes, "ClassExperience") ||
                    !SamePublicationAttribute(parsed.Attributes, session.Character.Attributes, "MsgNinja") ||
                    !graph.IsCurrent(session.PlayerItems)) return false;
                return ((ICollection<KeyValuePair<long, Guid>>)awaitingSessionPublication)
                    .Remove(new(session.AccountId, operation));
            });
        }
        catch (Exception error) when (IsWorkFailure(error)) { return false; }
    }

    private static bool SamePublicationAttribute(IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right, string key)
    {
        var had = left.TryGetValue(key, out var expected);
        return right.TryGetValue(key, out var actual) == had && (!had || string.Equals(expected, actual, StringComparison.Ordinal));
    }
}
