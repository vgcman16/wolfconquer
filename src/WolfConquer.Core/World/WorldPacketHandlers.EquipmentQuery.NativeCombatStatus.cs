using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        private QueryCombatStatusCapture? queryCombatStatus;

        // Capture only the exact managed packet that this invocation sends,
        // and only against its actual bound native Status object and holder.
        internal bool TryPrepareQueryCombatStatusFrame(out byte[] frame)
        {
            frame = [];
            if (!Refresh()) return false;
            var packet = CreateNativeMagicCachedPlayerStatusResponse(Context, Session,
                Context.Connection, Request.Scope.OwnerNativeClientIdentity);
            if (Session.NativeGameClient?.OrdinarySpellBinding is { } binding)
            {
                if (Context.Sessions is not { } world) return false;
                QueryCombatStatusCapture? captured = null;
                bool current = world.WithCurrentNativeClientIdentity(binding.Connection, binding.Uid, identity =>
                {
                    lock (binding.Client.SpellAdmissionSyncRoot)
                    lock (binding.Player.SpellAdmissionSyncRoot)
                    {
                        if (!ReferenceEquals(identity, Request.Scope.OwnerNativeClientIdentity) ||
                            !world.IsBoundSpellOwnerCurrent(binding) ||
                            !world.TryGet(binding.Connection, out var actual) || !ReferenceEquals(actual, Session) ||
                            binding.Client.Status is not { } status ||
                            !ReferenceEquals(status, binding.Connection.NativeGameClient?.Status)) return;
                        captured = new QueryCombatStatusCapture(binding, status, CombatFields.From(packet));
                    }
                });
                if (!current || captured is null) return false;
                queryCombatStatus = captured;
            }
            frame = packet.ToFrameBytes();
            return true;
        }

        private bool TryPrepareNativeCombatStatusCommit(LegacyOrdinarySpellBinding binding,
            out NativeQueryCombatCommit? prepared)
        {
            prepared = null;
            var capture = queryCombatStatus;
            var epoch = queryEpoch;
            if (capture is null || epoch is null || !ReferenceEquals(capture.Binding, binding) ||
                !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) ||
                !binding.Store.TryGet(binding.Connection, out var current) || !ReferenceEquals(current, Session) ||
                !ReferenceEquals(binding.Client.Status, capture.Status) || CombatFields.Read(capture.Status) != capture.Before)
                return false;
            var now = CreateNativeMagicCachedPlayerStatusResponse(Context, current,
                binding.Connection, Request.Scope.OwnerNativeClientIdentity);
            if (CombatFields.From(now) != capture.Sent || !ReferenceEquals(binding.Client.Status, capture.Status) ||
                CombatFields.Read(capture.Status) != capture.Before) return false;
            prepared = new NativeQueryCombatCommit();
            NativeQueryCombatCommits.Add(prepared, new(binding, current, epoch, capture.Status, capture.Before, capture.Sent));
            return true;
        }
    }

    internal readonly record struct CombatFields(uint MaxAttack, uint MinAttack, uint Defence,
        uint MagicAttack, uint MagicDefence)
    {
        internal static CombatFields From(LegacyPlayerStatusPacket packet) =>
            new(packet.MaxAttack, packet.MinAttack, packet.Defence, packet.MagicAttack, packet.MagicDefence);
        internal static CombatFields Read(LegacyNativeMsgStatus status) =>
            new(status.MaxAttack, status.MinAttack, status.Defence, status.MagicAttack, status.MagicDefence);
        internal void Write(LegacyNativeMsgStatus status)
        {
            status.MaxAttack = MaxAttack; status.MinAttack = MinAttack; status.Defence = Defence;
            status.MagicAttack = MagicAttack; status.MagicDefence = MagicDefence;
        }
    }

    private sealed class QueryCombatStatusCapture(LegacyOrdinarySpellBinding binding,
        LegacyNativeMsgStatus status, CombatFields sent)
    {
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyNativeMsgStatus Status = status;
        internal readonly CombatFields Before = CombatFields.Read(status);
        internal readonly CombatFields Sent = sent;
    }

    // Inert token. Only the private query executor above can associate it
    // with the actual emitted packet, epoch, holder and native Status.
    internal sealed class NativeQueryCombatCommit { }
    private sealed record NativeQueryCombatData(LegacyOrdinarySpellBinding Binding, WorldSession Session,
        LegacyNativeMagicQueryClient.QueryEpoch Epoch, LegacyNativeMsgStatus Status, CombatFields Before, CombatFields After);
    private static readonly ConditionalWeakTable<NativeQueryCombatCommit, NativeQueryCombatData> NativeQueryCombatCommits = new();

    // Consumed only by the real maximum publisher before any native writes or
    // published completion. A fabricated token or equal values have no association.
    internal static bool TryConsumeNativeQueryCombatCommit(NativeQueryCombatCommit token,
        LegacyOrdinarySpellBinding binding, WorldSession expected, LegacyNativeMagicQueryClient.QueryEpoch epoch,
        LegacyNativeMsgStatus status, out CombatFields after)
    {
        after = default;
        if (!NativeQueryCombatCommits.TryGetValue(token, out var data) ||
            !ReferenceEquals(data.Binding, binding) || !ReferenceEquals(data.Session, expected) ||
            !ReferenceEquals(data.Epoch, epoch) || !ReferenceEquals(data.Status, status) ||
            !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
            !binding.Store.IsBoundSpellOwnerCurrent(binding) ||
            !binding.Store.TryGet(binding.Connection, out var actual) || !ReferenceEquals(actual, expected) ||
            !ReferenceEquals(binding.Client.Status, status) || CombatFields.Read(status) != data.Before) return false;
        if (!NativeQueryCombatCommits.Remove(token)) return false;
        after = data.After;
        return true;
    }
}
