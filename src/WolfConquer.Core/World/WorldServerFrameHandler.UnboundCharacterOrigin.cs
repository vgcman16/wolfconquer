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
    internal sealed class UnboundOriginRegistration { }
    private sealed record OriginRegistrationData(ConquerTcpConnection Connection, WorldSession Initial, string Path, byte[] Image);
    private readonly ConditionalWeakTable<UnboundOriginRegistration, OriginRegistrationData> originRegistrations = new();

    private bool TryBindUnboundCharacterOrigin(ConquerTcpConnection connection, WorldSession initial,
        CharacterSnapshotLoader.ManagedOriginLoad receipt)
    {
        if (_snapshotLoader is null || connection.NativeGameClient is not null || initial.NativeGameClient is not null ||
            initial.AccountId != receipt.Result.AccountId || initial.Character.Uid != receipt.Result.Snapshot?.Uid) return false;
        return TryRegisterUnboundCharacterOrigin(connection, initial, receipt.Path, receipt.Image);
    }

    private bool TryBindUnboundAdmissionOrigin(ConquerTcpConnection connection, WorldSession admitted,
        LegacyOrdinaryAdmissionPlan plan)
    {
        if (connection.NativeGameClient is not null || admitted.NativeGameClient is not null) return true;
        return plan.TryCopyCommittedUnboundCharacterOrigin(this, _sessions, connection, admitted, out var path, out var image) &&
            TryRegisterUnboundCharacterOrigin(connection, admitted, path, image);
    }

    private bool TryRegisterUnboundCharacterOrigin(ConquerTcpConnection connection, WorldSession initial,
        string path, byte[] image)
    {
        if (_snapshotLoader is null || connection.NativeGameClient is not null || initial.NativeGameClient is not null ||
            !string.Equals(initial.UsersRoot, _snapshotLoader.UsersRoot, StringComparison.Ordinal) ||
            !string.Equals(Path.GetFullPath(path), Path.GetFullPath(Path.Combine(_snapshotLoader.UsersRoot,
                initial.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini")),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            unboundCharacterOrigins.TryGetValue(connection, out _)) return false;
        var registration = new UnboundOriginRegistration();
        originRegistrations.Add(registration, new(connection, initial, path, image.ToArray()));
        try { return _sessions.TryRegisterUnboundCharacterOrigin(this, connection, initial, registration); }
        finally { originRegistrations.Remove(registration); }
    }

    internal bool CompleteUnboundOriginRegistration(WorldSessionStore store, ConquerTcpConnection connection,
        WorldSession initial, UnboundOriginRegistration registration)
    {
        if (!ReferenceEquals(store, _sessions) || !originRegistrations.TryGetValue(registration, out var data) ||
            !ReferenceEquals(data.Connection, connection) || !ReferenceEquals(data.Initial, initial) ||
            !store.IsFramePersistenceCurrent(this, connection, initial) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(store, connection, initial.Character.Uid, out var identity)) return false;
        var input = new LegacyManagedCharacterImage(initial.Character);
        return CharacterSnapshotFileCoordinator.ExecuteLocked(data.Path, () =>
        {
            if (!CharacterSnapshotFileBaseline.TryReadBytes(data.Path, "managedOriginBind", out var bytes, out _) ||
                !bytes.AsSpan().SequenceEqual(data.Image) || !input.IsCurrent() ||
                !store.IsFramePersistenceCurrent(this, connection, initial)) return false;
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
            uint vigor = LegacyEquipmentQueryBounds.ReadVigor(initial.Character);
            if (!LegacyManagedCharacterImage.MatchesSaved(disk, initial.Character, vigor) ||
                LegacyEquipmentQueryBounds.ReadVigor(disk) != vigor) return false;
            unboundCharacterOrigins.Add(connection, new(identity, initial, data.Path, bytes));
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
