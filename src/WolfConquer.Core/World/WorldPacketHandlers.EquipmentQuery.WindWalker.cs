using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool WindWalkerViewerVisible(CharacterSnapshot owner, CharacterSnapshot viewer) => CanSeePlayer(owner, viewer);

    private sealed partial class LegacyEquipmentQueryExecution
    {
        internal async ValueTask<bool> RunWindWalkerPostludeAsync()
        {
            if (!Refresh() || Context.Trades?.HasConnectionEnded(Context.Connection) == true) return LoseWindWalkerOwner();
            if (Session.NativeGameClient?.OrdinarySpellBinding is null &&
                !LegacyPlayerDeathState.HasStatusFlag(Session.Character.Attributes, LegacyWindWalkerStatusProjection.Flag) &&
                !HasEffectiveFans(CapturedEquipment) && !HasEffectiveFans(LegacyEquipmentQuerySelector.Select(Session)))
                return true; // No194 effect needs no fabricated optional native owner.
            if (!LegacyNativeMagicQueryClient.TryCompleteWindWalker(Context.Sessions, Context.Connection,
                    Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity, queryEpoch, out var query) || query is null)
                return LoseWindWalkerOwner();
            if (Session.NativeGameClient?.OrdinarySpellBinding is null)
            {
                // Neutral legacy/loading compatibility needs no fabricated
                // native flag owner. A real fan effect still needs admission.
                if (Context.Sessions is { } world && Request.Scope.OwnerNativeClientIdentity is { } identity &&
                    LegacyNativeMagicQueryClient.TryValidateWindWalker(query, world, Context.Connection, identity, Session, out var desired) &&
                    !desired && !LegacyPlayerDeathState.HasStatusFlag(Session.Character.Attributes, LegacyWindWalkerStatusProjection.Flag)) return true;
                return LoseWindWalkerOwner();
            }
            if (Context.Sessions is not { } store ||
                !store.TryCommitWindWalkerQueryPostlude(Context, Session, Request.Scope, query, out var committed, out var publication) ||
                committed is null || publication is null) return LoseWindWalkerOwner();
            Adopt(RefreshLegacyEquipmentQueryPublication(Context with { Session = committed }, committed, Request.Scope));
            if (!Current.Completed) return false;
            var published = await publication.PublishAsync(CancellationToken).ConfigureAwait(false);
            if (published is null) return LoseWindWalkerOwner();
            Adopt(RefreshLegacyEquipmentQueryPublication(Context with { Session = published }, published, Request.Scope));
            return Current.Completed;
        }

        private bool LoseWindWalkerOwner()
        {
            Adopt(new(null, Current.RequiredParticipantSession, Current.Loss | LegacyEquipmentQueryPublicationLoss.OwnerLost));
            return false;
        }

        private static bool HasEffectiveFans(IReadOnlyList<PlayerItemSnapshotItem> selected) =>
            selected.Any(row => row.Durability != 0 && row.Position % 50 == 4 && row.ItemId is >= 626_003 and <= 626_439) &&
            selected.Any(row => row.Durability != 0 && row.Position % 50 == 5 && row.ItemId is >= 626_003 and <= 626_439);
    }
}
