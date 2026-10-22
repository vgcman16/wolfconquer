using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        public LegacyEquipmentQueryExecution(
            WorldPacketHandlerContext context, WorldSession session, LegacyEquipmentQueryRequest request)
        {
            Context = context with { Session = session };
            Session = session;
            var scope = request.Scope;
            if (context.Sessions is { } world)
            {
                if (scope.OwnerNativeClientIdentity is null)
                {
                    var ownerFound = LegacyNativeClientPublication.TryCaptureIdentity(
                        world, context.Connection, scope.OwnerUid, out var ownerIdentity);
                    scope = scope with { OwnerNativeClientIdentity = ownerFound ? ownerIdentity : new object() };
                }
                if (scope.RequiredParticipant is { } required && scope.RequiredNativeClientIdentity is null)
                {
                    var requiredFound = LegacyNativeClientPublication.TryCaptureIdentity(
                        world, required.Connection, required.Session.Character.Uid, out var requiredIdentity);
                    scope = scope with { RequiredNativeClientIdentity = requiredFound ? requiredIdentity : new object() };
                }
            }
            Request = request with { Scope = scope }; // BEFORE admission or AppendItems can await.
            Current = new(session, request.Scope.RequiredParticipant?.Session);
        }

        public WorldPacketHandlerContext Context { get; private set; }
        public WorldSession Session { get; private set; }
        public LegacyEquipmentQueryRequest Request { get; }
        public IReadOnlyList<PlayerItemSnapshotItem> CapturedEquipment { get; private set; } = [];
        public bool CapturedAlternate { get; private set; }
        public bool Admitted { get; set; }
        public LegacyEquipmentQueryPublicationResult Current { get; private set; }
        public LegacyEquipmentQueryPublicationReceipt Receipt { get; private set; }

        public bool Refresh()
        {
            Adopt(RefreshLegacyEquipmentQueryPublication(Context, Session, Request.Scope));
            return Current.Completed;
        }

        public bool CaptureEquipment()
        {
            if (!Refresh()) return false;
            CapturedEquipment = LegacyEquipmentQuerySelector.Select(Session);
            CapturedAlternate = Session.AlternateEquipmentActive;
            return true;
        }

        private void Adopt(LegacyEquipmentQueryPublicationResult result)
        {
            Current = result;
            if (result.Session is { } session)
            {
                Session = session;
                Context = Context with { Session = session };
            }
        }

        public bool Commit(Func<WorldSession, WorldSession> update)
        {
            Adopt(CommitLegacyEquipmentQueryMutation(Context, Session, Request.Scope, update));
            return Current.Completed;
        }

        public bool SetMaximum(uint maximum) => Commit(session => session with
        {
            EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(Request.Scope.OwnerUid, maximum),
        });

        public uint Maximum => GetLegacyEquipmentRuntimeMaxHitpoints(Session, Context.WorldData);

        public bool AddMaximum(uint addition)
        {
            if (!Refresh()) return false;
            return SetMaximum(unchecked(Maximum + addition));
        }

        public async ValueTask<bool> SendAsync(byte[] bytes, LegacyEquipmentQueryPublishedPhases phase)
        {
            if (!Refresh()) return false;
            await Context.Connection.SendAsync(bytes, CancellationToken).ConfigureAwait(false);
            if (!Refresh()) return false;
            Mark(phase);
            return true;
        }

        public CancellationToken CancellationToken { get; init; }

        public void Mark(LegacyEquipmentQueryPublishedPhases phase) =>
            Receipt = Receipt with { Phases = Receipt.Phases | phase };

        public async ValueTask<bool> AssignAsync(int value)
        {
            if (!Refresh()) return false;
            Adopt(await AssignLegacyPlayerHitpointsAsync(
                    Context, Session, Request.Scope, value, CancellationToken).ConfigureAwait(false));
            if (!Current.Completed) return false;
            Receipt = Receipt with
            {
                Phases = Receipt.Phases | LegacyEquipmentQueryPublishedPhases.Hitpoints,
                HitpointAssignments = Receipt.HitpointAssignments + 1,
                HitpointPairs = Receipt.HitpointPairs + 1,
            };
            return true;
        }

        public async ValueTask<bool> AddHitpointsAsync(uint addition)
        {
            if (!Refresh()) return false;
            return await AssignAsync(unchecked((int)Session.Character.MinHitPoints + (int)addition))
                .ConfigureAwait(false);
        }

        public async ValueTask<bool> PairAsync()
        {
            if (!Refresh()) return false;
            Adopt(await SendLegacyCurrentStatusHitpointPairAsync(
                    Context, Session, Request.Scope, CancellationToken).ConfigureAwait(false));
            if (!Current.Completed) return false;
            Receipt = Receipt with
            {
                Phases = Receipt.Phases | LegacyEquipmentQueryPublishedPhases.Hitpoints,
                HitpointPairs = Receipt.HitpointPairs + 1,
            };
            return true;
        }

        public LegacyEquipmentQueryOutcome Outcome() =>
            new(Current.Session, Current.RequiredParticipantSession, Admitted, Current.Loss, Receipt);
    }
}
