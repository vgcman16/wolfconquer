using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    private ManagedCharacterCommit? committedManagedCharacter;

    // Evidence of this writer's committed journal, not a capability to write or
    // to accept a new Character baseline. The Frame must still join it to its
    // own previously issued managed Character origin.
    internal sealed class ManagedCharacterCommit
    {
        private readonly LegacyPreparedSpellFileWrite writer;
        private readonly string itemsRoot;
        private readonly string itemsPath;
        private readonly LegacyTradeJournalChange[] changes;

        private ManagedCharacterCommit(LegacyPreparedSpellFileWrite writer, string itemsRoot,
            LegacyTradeJournalChange[] owned)
        {
            this.writer = writer;
            this.itemsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(itemsRoot));
            itemsPath = Path.Combine(this.itemsRoot, writer.expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            changes = owned.Select(change => new LegacyTradeJournalChange(change.Kind, change.FileId,
                change.Before?.ToArray(), change.After.ToArray())).ToArray();
        }

        internal static ManagedCharacterCommit Create(LegacyPreparedSpellFileWrite writer, string itemsRoot,
            LegacyTradeJournalChange[] owned) => new(writer, itemsRoot, owned);

        internal string CharacterPath => writer.CharacterPath;
        internal byte[] CopyCharacterBefore() => changes.Single(c => c.Kind == LegacyTradeJournalFileKind.Character).Before!.ToArray();
        internal byte[] CopyCharacterAfter() => changes.Single(c => c.Kind == LegacyTradeJournalFileKind.Character).After.ToArray();

        private bool ImagesCurrent() => SnapshotFileLockCoordinator.ExecuteLocked(
            [writer.CharacterPath, itemsPath, writer.SpellsPath], () =>
            {
                if (writer.Recovery.IsBlocked ||
                    !writer.Recovery.IsReadyForCharacterItemsSpells(writer.UsersRoot, itemsRoot, writer.SpellsRoot) ||
                    !writer.Recovery.TryRequireLiveJournalClear(out _)) return false;
                foreach (var change in changes)
                {
                    var path = change.Kind switch
                    {
                        LegacyTradeJournalFileKind.Character => writer.CharacterPath,
                        LegacyTradeJournalFileKind.Items => itemsPath,
                        LegacyTradeJournalFileKind.Spells => writer.SpellsPath,
                        _ => throw Unavailable()
                    };
                    var actual = ReadImage(path);
                    if (actual is null || !actual.AsSpan().SequenceEqual(change.After)) return false;
                }
                return true;
            });

        internal bool HasCurrentImages() => ImagesCurrent();
    }

    internal ManagedCharacterCommit RequireManagedCharacterCommit(WorldSessionStore actualStore,
        ConquerTcpConnection actualConnection, WorldSession actualExpected, WorldSession published)
    {
        var receipt = committedManagedCharacter;
        if (binding is not null || !committed || receipt is null || !ReferenceEquals(store, actualStore) ||
            !ReferenceEquals(connection, actualConnection) || !ReferenceEquals(expected, actualExpected) ||
            published.AccountId != expected.AccountId || published.Character.Uid != expected.Character.Uid ||
            !ReferenceEquals(connection.NativeGameClient, published.NativeGameClient) ||
            !ReferenceEquals(expected.NativeGameClient, published.NativeGameClient) ||
            connection.NativeGameClient?.OrdinarySpellBinding is not null ||
            !LegacyNativeClientPublication.TryCurrent(store, connection, expected.Character.Uid, identity, out var current) ||
            !ReferenceEquals(current, published)) throw Unavailable();
        var saved = PlayerSpellSnapshotLoader.Parse(after!);
        if (!PlayerSpellSnapshotLoader.ToLegacyBytes(saved).AsSpan()
                .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(published.PlayerSpells)) ||
            !receipt.HasCurrentImages() ||
            !LegacyNativeClientPublication.TryCurrent(store, connection, expected.Character.Uid, identity, out current) ||
            !ReferenceEquals(current, published) || !ReferenceEquals(committedManagedCharacter, receipt)) throw Unavailable();
        return receipt;
    }
}
