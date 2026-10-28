using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal LegacyPreparedSpellFileWrite PrepareSpellFileWrite(ConquerTcpConnection connection, WorldSession expected,
        LegacyTradeRecoveryState recovery, string usersRoot, string spellsRoot)
    {
        if (IsCurrentThreadInsideAnyConnectionGate)
            return Capture();
        RefuseBoundSpellReentry(connection);
        using var packageScope = LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected);
        if (!_connectionGates.TryGetValue(connection, out var gate)) throw InvalidSpellPublication();
        lock (gate)
        {
            using var scope = EnterNativeConnectionGate();
            return Capture();
        }
        LegacyPreparedSpellFileWrite Capture()
        {
            if (!_connectionGates.TryGetValue(connection, out var holderGate) || !Monitor.IsEntered(holderGate) ||
                !_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                !ReferenceEquals(connection.NativeGameClient, expected.NativeGameClient) ||
                !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) throw InvalidSpellPublication();
            var binding = holder.SpellBinding;
            if (binding is not null && !BindingIsCurrentPlain(binding, holder, expected) || binding is null &&
                (connection.NativeGameClient?.OrdinarySpellBinding is not null || expected.NativeGameClient?.OrdinarySpellBinding is not null))
                throw InvalidSpellPublication();
            return LegacyPreparedSpellFileWrite.Capture(this, connection, expected, holder.NativeClientIdentity,
                binding, recovery, usersRoot, spellsRoot);
        }
    }

    internal bool IsSpellFileWriteOwnerCurrent(ConquerTcpConnection connection, WorldSession expected, object identity,
        LegacyOrdinarySpellBinding? binding) =>
        _connectionGates.TryGetValue(connection, out var gate) && Monitor.IsEntered(gate) &&
        _sessions.TryGetValue(connection, out var holder) && ReferenceEquals(holder.Read(), expected) &&
        ReferenceEquals(holder.NativeClientIdentity, identity) && ReferenceEquals(holder.SpellBinding, binding) &&
        (binding is null ? connection.NativeGameClient?.OrdinarySpellBinding is null &&
            expected.NativeGameClient?.OrdinarySpellBinding is null : BindingIsCurrentPlain(binding, holder, expected));
}
