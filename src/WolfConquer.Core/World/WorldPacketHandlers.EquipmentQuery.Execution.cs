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
            Request = request;
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
            if (!Refresh()) return false;
            Session = update(Session);
            Context = Context with { Session = Session };
            Context.Sessions?.Set(Context.Connection, Session);
            return Refresh();
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
