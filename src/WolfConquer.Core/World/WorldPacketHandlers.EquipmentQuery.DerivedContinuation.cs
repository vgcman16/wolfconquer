using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly ConditionalWeakTable<OwnedDerivedSpellQueryPreparation, PendingDerivedQuerySource> PendingDerivedQueries = new();

    internal sealed partial class OwnedDerivedSpellQueryPreparation
    {
        internal bool PendingSourceIsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            PendingDerivedQueries.TryGetValue(this, out var pending)
                ? pending.IsCurrentPlain(binding)
                : LegacyExperienceContinuationCodec.ReadCharacter(Expected.Character, Expected.AccountId) is null;

        internal void BindPendingWriter(LegacyPreparedDerivedSpellQueryWrite writer, LegacyExperienceContinuation pending)
        {
            if (!PendingDerivedQueries.TryGetValue(this, out var source) || !Equals(source.Pending, pending) ||
                source.Writer is not null || !writer.CapturesPendingParticipant(this, pending)) throw InvalidDerivedQueryPreparation();
            source.Writer = writer;
        }

        internal bool PendingWriterIsCurrentPlain(LegacyOrdinarySpellBinding binding, LegacyPreparedDerivedSpellQueryWrite writer,
            LegacyExperienceContinuation pending, bool requireConsumed = false) =>
            PendingDerivedQueries.TryGetValue(this, out var source) && ReferenceEquals(source.Writer, writer) &&
            Equals(source.Pending, pending) && (!requireConsumed || Volatile.Read(ref source.Consumed) == 1) &&
            source.IsCurrentPlain(binding) && writer.CapturesPendingParticipant(this, pending);

        internal bool TryConsumePendingWriter(LegacyOrdinarySpellBinding binding, LegacyPreparedDerivedSpellQueryWrite writer,
            LegacyExperienceContinuation pending) => PendingWriterIsCurrentPlain(binding, writer, pending) &&
            PendingDerivedQueries.TryGetValue(this, out var source) && Interlocked.CompareExchange(ref source.Consumed, 1, 0) == 0;

        internal bool PendingPublicationIsCurrentPlain(LegacyOrdinarySpellBinding binding,
            LegacyPreparedDerivedSpellQueryWrite writer, WorldSession actual, LegacyExperienceContinuation pending) =>
            PendingDerivedQueries.TryGetValue(this, out var source) && ReferenceEquals(source.Writer, writer) &&
            Volatile.Read(ref source.Consumed) == 1 && Equals(source.Pending, pending) &&
            source.IsPublishedCurrentPlain(binding, actual) && writer.CapturesPendingParticipant(this, pending);
    }

    private sealed class PendingDerivedQuerySource
    {
        private readonly LegacyEquipmentQueryExecution execution;
        private readonly OwnedDerivedSpellQueryPreparation preparation;
        private readonly LegacyEquipmentQueryPublicationScope scope;
        private readonly WorldSession projected;
        private readonly LegacyNativeAttributeMapProof beforeAttributes, afterAttributes, beforePaid, afterPaid;
        internal readonly LegacyExperienceContinuation Pending;
        internal LegacyPreparedDerivedSpellQueryWrite? Writer;
        internal int Consumed;

        internal PendingDerivedQuerySource(LegacyEquipmentQueryExecution execution, OwnedDerivedSpellQueryPreparation preparation,
            LegacyExperienceContinuation pending)
        {
            this.execution = execution; this.preparation = preparation; Pending = pending; scope = execution.Request.Scope;
            projected = preparation.Expected with { Character = preparation.Model.Character, PlayerSpells = preparation.Model.Spells };
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(preparation.Expected.Character.Attributes);
            beforePaid = LegacyNativeAttributeMapProof.CaptureRaw(preparation.Expected.Character.PaidNobility);
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(preparation.Model.Character.Attributes);
            afterPaid = LegacyNativeAttributeMapProof.CaptureRaw(preparation.Model.Character.PaidNobility);
            if (!Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(preparation.Model.Character, preparation.Expected.AccountId)))
                throw InvalidDerivedQueryPreparation();
        }

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            execution.IsPendingDerivedCurrent(this, preparation, published: null) && MapsCurrent() &&
            ReferenceEquals(binding, preparation.Expected.NativeGameClient?.OrdinarySpellBinding) &&
            Equals(Pending, LegacyExperienceContinuationCodec.ReadCharacter(preparation.Expected.Character, preparation.Expected.AccountId)) &&
            Covers(preparation.Context, preparation.Expected, projected);

        internal bool IsPublishedCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession actual) =>
            execution.IsPendingDerivedCurrent(this, preparation, actual) && MapsCurrent() &&
            ReferenceEquals(actual.NativeGameClient, binding.Client) &&
            Equals(Pending, LegacyExperienceContinuationCodec.ReadCharacter(actual.Character, actual.AccountId)) &&
            Covers(preparation.Context, actual, actual);

        private bool MapsCurrent() => beforeAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() &&
            afterAttributes.IsCurrentPlain() && afterPaid.IsCurrentPlain();

        private bool Covers(WorldPacketHandlerContext context, WorldSession before, WorldSession after) =>
            scope.LevelUpRebuild is { } levelUp ? scope.AdmissionContinuation is null &&
                levelUp.CoversPendingQueryProjection(context, before, after, Pending) :
                scope.AdmissionContinuation?.CoversPendingQueryProjection(context, before, after, Pending) == true;
    }

    private sealed partial class LegacyEquipmentQueryExecution
    {
        private PendingDerivedQuerySource? activePendingDerived;

        private void BeginPendingDerived(OwnedDerivedSpellQueryPreparation preparation)
        {
            var pending = LegacyExperienceContinuationCodec.ReadCharacter(Session.Character, Session.AccountId);
            if (pending is null) return;
            if (activePendingDerived is not null || !Equals(pending, PendingContinuation()) || !HasCurrentContinuationInvocation())
                throw InvalidDerivedQueryPreparation();
            var source = new PendingDerivedQuerySource(this, preparation, pending);
            PendingDerivedQueries.Add(preparation, source);
            activePendingDerived = source;
        }

        internal bool IsPendingDerivedCurrent(PendingDerivedQuerySource source, OwnedDerivedSpellQueryPreparation preparation,
            WorldSession? published) => ReferenceEquals(activePendingDerived, source) && Current.Completed &&
            HasCurrentContinuationInvocation() && ReferenceEquals(queryEpoch, preparation.Epoch) &&
            (published is null ? ReferenceEquals(Session, preparation.Expected) && ReferenceEquals(Context, preparation.Context) :
                ReferenceEquals(Session, preparation.Expected) || ReferenceEquals(Session, published)) &&
            ReferenceEquals(Request.Scope.OwnerNativeClientIdentity, preparation.Expected.NativeGameClient?.OrdinarySpellBinding?.NativeIdentity);

        private void EndPendingDerived() => activePendingDerived = null;
    }
}
