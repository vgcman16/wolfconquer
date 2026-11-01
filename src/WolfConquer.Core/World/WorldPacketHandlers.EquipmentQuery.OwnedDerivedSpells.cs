using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Empty instances have no registration and cannot authorize a write.
    internal sealed class OwnedDerivedSpellQueryPreparation
    {
        private OwnedDerivedQueryData Data => DerivedQueryPreparations.TryGetValue(this, out var data) ? data :
            throw InvalidDerivedQueryPreparation();
        internal WorldPacketHandlerContext Context => Data.Context;
        internal WorldSession Expected => Data.Expected;
        internal LegacyDerivedSpellQuerySource Source => Data.Source;
        internal LegacyDerivedSpellDesiredRow Desired => Data.Desired;
        internal LegacyDerivedSpellReconciliation.Model Model => Data.Model;
        internal LegacyNativeMagicQueryClient.QueryEpoch Epoch => Data.Epoch;
        internal byte[] CopyFrame() => Data.Frame.ToArray();
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            DerivedQueryPreparations.TryGetValue(this, out var data) && data.IsCurrentPlain(binding);
    }

    private static readonly ConditionalWeakTable<OwnedDerivedSpellQueryPreparation, OwnedDerivedQueryData> DerivedQueryPreparations = new();
    private sealed class OwnedDerivedQueryData
    {
        private readonly LegacyEquipmentQueryExecution execution;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Expected;
        internal readonly LegacyDerivedSpellQuerySource Source;
        internal readonly LegacyDerivedSpellDesiredRow Desired;
        internal readonly LegacyDerivedSpellReconciliation.Model Model;
        internal readonly LegacyNativeMagicQueryClient.QueryEpoch Epoch;
        internal readonly byte[] Frame;

        internal OwnedDerivedQueryData(LegacyEquipmentQueryExecution execution, WorldPacketHandlerContext context,
            WorldSession expected, LegacyDerivedSpellQuerySource source, LegacyDerivedSpellDesiredRow desired,
            LegacyDerivedSpellReconciliation.Model model, LegacyNativeMagicQueryClient.QueryEpoch epoch)
        {
            this.execution = execution; Context = context; Expected = expected; Source = source;
            Desired = desired; Model = model; Epoch = epoch;
            Frame = desired.After is { } grant ? LegacySpellPacket.FromSnapshot(grant).ToFrameBytes() :
                new MsgActionPacket { ObjId = checked((uint)expected.Character.Uid), DwParam = desired.Id,
                    Type = (MsgActionType)109 }.ToFrameBytes();
        }

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            execution.IsDerivedQuerySourceCurrentPlain(Context, Expected, Epoch) &&
            ReferenceEquals(binding.Store, Context.Sessions) && ReferenceEquals(binding.Connection, Context.Connection) &&
            ReferenceEquals(binding.ItemCatalog, Context.WorldData) && ReferenceEquals(Expected.NativeGameClient, binding.Client) &&
            binding.AccountId == Expected.AccountId && binding.Uid == Expected.Character.Uid &&
            Context.Trades is { } trades && Monitor.IsEntered(trades.SyncRoot) &&
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            binding.Store.TryGet(binding.Connection, out var held) && ReferenceEquals(held, Expected) &&
            binding.Store.IsBoundSpellOwnerCurrent(binding);
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        // Read-only proof of THIS actual private invocation, never a caller's
        // detached Context or a public completion/acceptance callback.
        internal bool IsDerivedQuerySourceCurrentPlain(WorldPacketHandlerContext original, WorldSession expected,
            LegacyNativeMagicQueryClient.QueryEpoch epoch)
        {
            if (!Current.Completed || !ReferenceEquals(Session, expected) || !ReferenceEquals(Context, original) ||
                !ReferenceEquals(queryEpoch, epoch) || Request.Scope.OwnerUid != expected.Character.Uid ||
                expected.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
                !ReferenceEquals(Request.Scope.OwnerNativeClientIdentity, binding.NativeIdentity)) return false;
            if (Request.Scope.RequiredParticipant is not { } required) return true;
            return Context.Sessions is { } store && Context.Trades is { } trades &&
                !trades.HasConnectionEnded(required.Connection) && store.TryGet(required.Connection, out var peer) &&
                ReferenceEquals(peer, Current.RequiredParticipantSession) && peer?.NativeGameClient?.OrdinarySpellBinding is { } peerBinding &&
                ReferenceEquals(Request.Scope.RequiredNativeClientIdentity, peerBinding.NativeIdentity) &&
                store.IsBoundSpellOwnerCurrent(peerBinding);
        }

        private async ValueTask<bool> CommitOwnedDerivedSpellQueryAsync(LegacyDerivedSpellDesiredRow desired)
        {
            if (!Refresh() || Context.Sessions is not { } store || Context.Trades is not { } trades ||
                Session.NativeGameClient?.OrdinarySpellBinding is null || queryEpoch is null) return false;
            var model = LegacyDerivedSpellReconciliation.Project(Session.Character, Session.PlayerSpells, new[] { desired });
            if (model.Changes.Count == 0) return true; // No unmarked adoption and no unused row lookup.
            var source = desired.Id switch
            { 21_930 => LegacyDerivedSpellQuerySource.Rank, 20_000 => LegacyDerivedSpellQuerySource.MythSoul,
                _ => throw InvalidDerivedQueryPreparation() };
            var token = new OwnedDerivedSpellQueryPreparation();
            DerivedQueryPreparations.Add(token, new(this, Context, Session, source, desired, model, queryEpoch));
            WorldSessionStore.DerivedSpellQueryPublication? publication;
            string reason;
            bool committed;
            lock (trades.SyncRoot)
                committed = store.TryCommitOwnedDerivedSpellQuery(Context, token, out publication, out reason);
            if (!committed || publication is null)
            {
                Context.Log.WriteLine("WorldServer held owned derived spell query phase reason=" + reason + ".");
                return false;
            }
            Adopt(RefreshLegacyEquipmentQueryPublication(Context with { Session = publication.Published }, publication.Published, Request.Scope));
            if (!Current.Completed || !await publication.SendAsync(CancellationToken).ConfigureAwait(false)) return false;
            return Refresh();
        }
    }

    private static InvalidOperationException InvalidDerivedQueryPreparation() => new("derivedSpellQuery23RequiresActualPrivateExecution");
}
