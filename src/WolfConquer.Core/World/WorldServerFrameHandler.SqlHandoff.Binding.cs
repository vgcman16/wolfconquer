using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly ConditionalWeakTable<ConquerTcpConnection, SqlConnectionHandoffBinding> _sqlConnectionHandoffs = new();

    private bool HasSqlConnectionHandoffFence(ConquerTcpConnection connection) =>
        _sqlConnectionHandoffs.TryGetValue(connection, out _);

    private sealed class SqlConnectionHandoffBinding
    {
        internal readonly WorldServerFrameHandler Frame;
        internal readonly ConquerTcpConnection Connection;
        internal readonly CreationConnectionLifetime Lifetime;
        internal readonly LegacyNativeGameClientRuntime Native;
        internal readonly LegacyNativePlayerRuntime Player;
        // All mutable fields are accessed under this Frame's actual world lease.
        internal SqlWorldBundleCustody? Custody;
        internal SqlAccountWorldHandoffException? PendingCleanup;
        internal ExceptionDispatchInfo? Failure;
        internal long AccountId;
        internal uint CharacterUid;
        internal string? Username;
        internal bool WasRedeemed, IsRetained;

        internal SqlConnectionHandoffBinding(WorldServerFrameHandler frame,
            ConquerTcpConnection connection, CreationConnectionLifetime lifetime)
        {
            Frame = frame; Connection = connection; Lifetime = lifetime;
            Native = connection.NativeGameClient!; Player = Native.Player;
        }
    }

    private void RequireSqlConnectionHandoffCurrent(SqlConnectionHandoffBinding binding)
    {
        var connection = binding.Connection;
        if (!ReferenceEquals(binding.Frame, this) ||
            !_sqlConnectionHandoffs.TryGetValue(connection, out var current) || !ReferenceEquals(current, binding) ||
            !_creationLifetimes.TryGetValue(connection, out var lifetime) || !ReferenceEquals(lifetime, binding.Lifetime) ||
            !lifetime.IsCurrent(connection) || connection.HasObservedTransportClose || !connection.HasRegisteredRelicWire ||
            !ReferenceEquals(connection.NativeGameClient, binding.Native) || !ReferenceEquals(binding.Native.Player, binding.Player) ||
            !ReferenceEquals(binding.Player.Owner, binding.Native) || binding.Native.FullLoading || binding.Native.Fake ||
            !IsPreCharacterConnection(connection) || _successfulInternalHandoffs.TryGetValue(connection, out _) ||
            _tradeRecovery.IsBlocked || !_tradeRecovery.TryRequireLiveJournalClear(out _))
            throw new InvalidOperationException("sqlConnectionHandoffNoLongerCurrent");
    }

    private static void RequireSqlConnectionHandoffRead(SqlConnectionHandoffBinding binding,
        SqlWorldStateBundleReadResult read)
    {
        if (binding.Custody is not { } custody || !binding.WasRedeemed || binding.AccountId <= 0 ||
            binding.CharacterUid == 0 || string.IsNullOrWhiteSpace(binding.Username) ||
            custody.AccountId != binding.AccountId || custody.CharacterUid != binding.CharacterUid ||
            read.Status != SqlWorldStateReadStatus.Found || read.Record is not { } record ||
            record.Image.AccountId != binding.AccountId || record.Image.CharacterUid != binding.CharacterUid)
            throw new InvalidDataException("sqlConnectionHandoffWorldIdentityUnavailable");
    }

    private static Exception SqlConnectionCommittedFailure(Exception failure) =>
        failure is SqlConnectionHandoffCommittedException ? failure : new SqlConnectionHandoffCommittedException(failure);
}
