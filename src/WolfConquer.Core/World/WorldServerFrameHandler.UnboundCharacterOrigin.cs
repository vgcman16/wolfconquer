using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Private registration is the authority; neither this data object nor an
    // independently constructed empty token can register a managed owner.
    private sealed class UnboundCharacterOrigin
    {
        internal readonly object Identity;
        internal readonly long AccountId, Uid;
        internal readonly string Path;
        internal byte[] Image;
        internal uint RuntimeVigor;
        internal UnboundCharacterOrigin(object identity, WorldSession initial, string path, byte[] image)
        {
            Identity = identity; AccountId = initial.AccountId; Uid = initial.Character.Uid;
            Path = path; Image = image.ToArray(); RuntimeVigor = LegacyEquipmentQueryBounds.ReadVigor(initial.Character);
        }
    }

    private readonly ConditionalWeakTable<ConquerTcpConnection, UnboundCharacterOrigin> unboundCharacterOrigins = new();

    private bool TryBindUnboundCharacterOrigin(ConquerTcpConnection connection, WorldSession initial,
        CharacterSnapshotLoader.ManagedOriginLoad receipt)
    {
        if (_snapshotLoader is null || connection.NativeGameClient is not null || initial.NativeGameClient is not null ||
            initial.AccountId != receipt.Result.AccountId || initial.Character.Uid != receipt.Result.Snapshot?.Uid ||
            !string.Equals(initial.UsersRoot, _snapshotLoader.UsersRoot, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(receipt.Path), Path.GetFullPath(Path.Combine(_snapshotLoader.UsersRoot,
                initial.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini")),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            unboundCharacterOrigins.TryGetValue(connection, out _) ||
            !_sessions.TryGet(connection, out var actual) || !ReferenceEquals(actual, initial) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(_sessions, connection, initial.Character.Uid, out var identity)) return false;
        var input = new LegacyManagedCharacterImage(initial.Character);
        return CharacterSnapshotFileCoordinator.ExecuteLocked(receipt.Path, () =>
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(receipt.Path, "managedOriginBind", out var bytes, out _) ||
                !bytes.AsSpan().SequenceEqual(receipt.Image) || !input.IsCurrent() ||
                !LegacyNativeClientPublication.TryCurrent(_sessions, connection, initial.Character.Uid, identity, out var current) ||
                !ReferenceEquals(current, initial)) return false;
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
            uint vigor = LegacyEquipmentQueryBounds.ReadVigor(initial.Character);
            if (!LegacyManagedCharacterImage.MatchesSaved(disk, initial.Character, vigor) ||
                LegacyEquipmentQueryBounds.ReadVigor(disk) != vigor) return false;
            unboundCharacterOrigins.Add(connection, new(identity, initial, receipt.Path, bytes));
            return true;
        });
    }

    internal bool RequiresUnboundCharacterOrigin(WorldSession current) => _snapshotLoader is not null &&
        current.NativeGameClient is null && !string.IsNullOrWhiteSpace(current.UsersRoot) &&
        string.Equals(current.UsersRoot, _snapshotLoader.UsersRoot, StringComparison.Ordinal);

    private UnboundCharacterOrigin RequireUnboundCharacterOrigin(WorldSessionStore store,
        ConquerTcpConnection connection, WorldSession expected)
    {
        if (!ReferenceEquals(_sessions, store) || _tradeRecovery.IsBlocked || connection.NativeGameClient is not null ||
            !store.IsFramePersistenceCurrent(this, connection, expected) ||
            !unboundCharacterOrigins.TryGetValue(connection, out var origin) ||
            origin.AccountId != expected.AccountId || origin.Uid != expected.Character.Uid ||
            !LegacyNativeClientPublication.TryCurrent(store, connection, origin.Uid, origin.Identity, out var actual) ||
            !ReferenceEquals(actual, expected)) throw new InvalidOperationException("managedCharacterOriginUnavailable");
        return origin;
    }
}
