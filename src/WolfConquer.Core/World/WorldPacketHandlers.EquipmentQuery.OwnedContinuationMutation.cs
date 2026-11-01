using System.Globalization;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Empty tokens have no registration. The Store receives concrete images,
    // never the execution's internal projection function or a borrowed scope.
    internal sealed class OwnedContinuationQueryMutation
    {
        private ContinuationQueryMutationData Data => ContinuationQueryMutations.TryGetValue(this, out var data)
            ? data : throw InvalidContinuationQueryMutation();
        internal WorldPacketHandlerContext Context => Data.Context;
        internal WorldSession Expected => Data.Expected;
        internal WorldSession Proposed => Data.Proposed;
        internal LegacyExperienceContinuation Pending => Data.Pending;
        internal string CharacterPath => Data.CharacterPath;
        internal void ArmFinalProof(LegacyOrdinarySpellBinding binding) => Data.Arm(binding);
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            ContinuationQueryMutations.TryGetValue(this, out var data) && Volatile.Read(ref data.Consumed) == 0 && data.IsCurrentPlain(binding);
        internal bool TryConsumeBeforeWriter(LegacyOrdinarySpellBinding binding) =>
            ContinuationQueryMutations.TryGetValue(this, out var data) && data.Armed && data.IsCurrentPlain(binding) &&
            Interlocked.CompareExchange(ref data.Consumed, 1, 0) == 0;
        internal bool IsConsumedCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            ContinuationQueryMutations.TryGetValue(this, out var data) && data.Armed &&
            Volatile.Read(ref data.Consumed) == 1 && data.IsCurrentPlain(binding);
    }

    private static readonly ConditionalWeakTable<OwnedContinuationQueryMutation, ContinuationQueryMutationData> ContinuationQueryMutations = new();

    private sealed class ContinuationQueryMutationData
    {
        private readonly LegacyEquipmentQueryExecution execution;
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyEquipmentQueryPublicationScope scope;
        private readonly LegacyNativeMagicQueryClient.QueryEpoch? epoch;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, afterAttributes, beforePaid, afterPaid;
        private readonly LegacyItemSaveGraph items;
        private LegacyNativeMagicQueryClient.DerivedSpellQuerySnapshot? native;
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Expected, Proposed;
        internal readonly LegacyExperienceContinuation Pending;
        internal readonly string CharacterPath;
        internal bool Armed { get; private set; }
        internal int Consumed;

        internal ContinuationQueryMutationData(LegacyEquipmentQueryExecution execution, LegacyOrdinarySpellBinding binding,
            WorldSession proposed, LegacyExperienceContinuation pending, LegacyNativeMagicQueryClient.QueryEpoch? epoch)
        {
            this.execution = execution; this.binding = binding; this.epoch = epoch;
            Context = execution.Context; Expected = execution.Session; scope = execution.Request.Scope;
            Proposed = proposed; Pending = pending;
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Expected.Character.Attributes);
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.Attributes);
            beforePaid = LegacyNativeAttributeMapProof.CaptureRaw(Expected.Character.PaidNobility);
            afterPaid = LegacyNativeAttributeMapProof.CaptureRaw(proposed.Character.PaidNobility);
            if (!LegacyItemSaveGraph.TryCapture(Expected.PlayerItems, out var captured) || captured is null ||
                !ReferenceEquals(Expected.PlayerItems, proposed.PlayerItems) || !ReferenceEquals(Expected.PlayerSpells, proposed.PlayerSpells) ||
                !ReferenceEquals(Expected.PlayerProficiencies, proposed.PlayerProficiencies) ||
                Expected.AlternateEquipmentActive != proposed.AlternateEquipmentActive ||
                !Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(proposed.Character, proposed.AccountId)))
                throw InvalidContinuationQueryMutation();
            items = captured;
            CharacterPath = Path.Combine(binding.UsersRoot, binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        }

        internal void Arm(LegacyOrdinarySpellBinding actual)
        {
            if (Armed || Volatile.Read(ref Consumed) != 0 || !IsCurrentPlain(actual)) throw InvalidContinuationQueryMutation();
            // Genuine admission/reset-prefix mutations precede the private
            // magic epoch. Later phases retain that actual epoch and contents.
            if (epoch is not null) native = LegacyNativeMagicQueryClient.DerivedSpellQuerySnapshot.Capture(actual, Expected, epoch);
            Armed = true;
        }

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding actual) => ReferenceEquals(actual, binding) &&
            execution.IsContinuationMutationCurrent(this, Context, Expected, scope, epoch) &&
            Context.Trades is { } trades && Monitor.IsEntered(trades.SyncRoot) &&
            WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate &&
            Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
            ReferenceEquals(binding.Store, Context.Sessions) && ReferenceEquals(binding.Connection, Context.Connection) &&
            ReferenceEquals(binding.ItemCatalog, Context.WorldData) && ReferenceEquals(Expected.NativeGameClient, binding.Client) &&
            binding.AccountId == Expected.AccountId && binding.Uid == Expected.Character.Uid &&
            beforeAttributes.IsCurrentPlain() && afterAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() && afterPaid.IsCurrentPlain() &&
            items.IsCurrent(Expected.PlayerItems) && items.IsCurrent(Proposed.PlayerItems) &&
            Equals(Pending, LegacyExperienceContinuationCodec.ReadCharacter(Expected.Character, Expected.AccountId)) &&
            CoversPendingProjection() &&
            (!Armed || epoch is null || native?.IsBeforeCurrentPlain(binding, Expected) == true);

        private bool CoversPendingProjection() => scope.LevelUpRebuild is { } levelUp
            ? scope.AdmissionContinuation is null && levelUp.CoversPendingQueryProjection(Context, Expected, Proposed, Pending)
            : scope.AdmissionContinuation?.CoversPendingQueryProjection(Context, Expected, Proposed, Pending) == true;
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        private ContinuationQueryMutationData? activeContinuationMutation;

        private LegacyExperienceContinuation? PendingContinuation()
        {
            if (continuationInvocation is null) return null; // Ordinary query path is unchanged.
            if (!HasCurrentContinuationInvocation()) throw InvalidContinuationQueryMutation();
            return LegacyExperienceContinuationCodec.ReadCharacter(Session.Character, Session.AccountId)
                ?? throw InvalidContinuationQueryMutation();
        }

        internal bool IsContinuationMutationCurrent(ContinuationQueryMutationData data, WorldPacketHandlerContext context,
            WorldSession expected, LegacyEquipmentQueryPublicationScope scope, LegacyNativeMagicQueryClient.QueryEpoch? epoch) =>
            ReferenceEquals(activeContinuationMutation, data) && Current.Completed && HasCurrentContinuationInvocation() &&
            ReferenceEquals(Context, context) && ReferenceEquals(Session, expected) && ReferenceEquals(Request.Scope, scope) &&
            ReferenceEquals(queryEpoch, epoch) && (scope.LevelUpRebuild is not null && scope.AdmissionContinuation is null
                ? Request.Reason == LegacyEquipmentRebuildReason.LevelUp && !Request.CallItems
                : scope.AdmissionContinuation is not null && scope.LevelUpRebuild is null &&
                    Request.Reason == LegacyEquipmentRebuildReason.InitialWorldEntry && Request.CallItems);

        private bool CommitContinuationMutation(Func<WorldSession, WorldSession> project, LegacyExperienceContinuation pending)
        {
            if (!Refresh() || activeContinuationMutation is not null || Context.Sessions is not { } store ||
                Session.NativeGameClient?.OrdinarySpellBinding is not { } binding) return false;
            WorldSession proposed = project(Session); // Only private execution code supplies this calculation.
            if (proposed.Character.MinHitPoints != Session.Character.MinHitPoints || proposed.Character.MinMana != Session.Character.MinMana)
                proposed = proposed with { Character = new BoundHitpointsInputs(Session.Character, proposed.Character).After };
            var proposal = new OwnedContinuationQueryMutation();
            var data = new ContinuationQueryMutationData(this, binding, proposed, pending, queryEpoch);
            ContinuationQueryMutations.Add(proposal, data);
            activeContinuationMutation = data;
            try
            {
                if (!store.TryCommitContinuationQueryMutation(proposal, out var final) || final is null)
                { RefuseIncompleteQuery(); return false; }
                Adopt(RefreshLegacyEquipmentQueryPublication(Context with { Session = final }, final, Request.Scope));
                return Current.Completed;
            }
            finally { activeContinuationMutation = null; }
        }

        private async ValueTask<bool> AssignContinuationHitpointsAsync(int value)
        {
            if (!Commit(session => ProjectAssignedLegacyHitpoints(Context, session, Request.Scope.OwnerUid, value))) return false;
            Adopt(await SendLegacyAssignedHitpointsTeamInfoAsync(Context, Session, Request.Scope, value, CancellationToken).ConfigureAwait(false));
            if (!Current.Completed) return false;
            Adopt(await SendLegacyCurrentStatusHitpointPairAsync(Context, Session, Request.Scope, CancellationToken).ConfigureAwait(false));
            return Current.Completed;
        }
    }

    private static InvalidOperationException InvalidContinuationQueryMutation() => new("continuationQueryMutationRequiresPrivateExecution");
}
