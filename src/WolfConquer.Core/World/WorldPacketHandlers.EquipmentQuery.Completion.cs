namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        internal void RefuseIncompleteQuery() => Adopt(Current with
            { Loss = Current.Loss | LegacyEquipmentQueryPublicationLoss.OwnerLost });

        internal bool PublishCompletedQueryMaximums()
        {
            if (!Refresh()) return false;
            if (Session.NativeGameClient?.OrdinarySpellBinding is not { } binding) return true;
            if (Context.Sessions is not { } world || Context.Trades is not { } trades) return false;
            bool completed = false;
            lock (trades.SyncRoot)
                world.WithCurrentNativeClientIdentity(binding.Connection, binding.Uid, identity =>
                {
                    if (!ReferenceEquals(identity, Request.Scope.OwnerNativeClientIdentity)) return;
                    lock (binding.Client.SpellAdmissionSyncRoot)
                    lock (binding.Player.SpellAdmissionSyncRoot)
                        completed = LegacyNativeMagicQueryClient.PublishCompletedMaximums(binding, Session, queryEpoch);
                });
            return completed;
        }
    }
}
