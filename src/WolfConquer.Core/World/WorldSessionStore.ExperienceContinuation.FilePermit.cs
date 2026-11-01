using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed record ContinuationFileSource(WorldSessionStore Store, WorldPacketHandlerContext Context,
        LegacyOrdinarySpellBinding Binding, WorldSession Expected, string Path, byte[] Before, byte[] After,
        WorldPacketHandlers.OwnedCombatExperience? Credit, ExperienceContinuationExecution.StepPlan? Step,
        WorldPacketHandlers.CompletedContinuationQuery? Query, ExperienceContinuationExecution? Clear,
        WorldPacketHandlers.OwnedContinuationQueryMutation? Mutation);
    private static readonly ConditionalWeakTable<ContinuationFilePermit, ContinuationFileSource> ContinuationFilePermits = new();

    // Private issuers are called only after the actual writer's own typed
    // operation proof and exact-image preparation, while its owner gates are held.
    private static ContinuationFilePermit IssueContinuationFilePermit(WorldSessionStore store, WorldPacketHandlerContext context,
        LegacyOrdinarySpellBinding binding, WorldSession expected, string path, byte[] before, byte[] after,
        WorldPacketHandlers.OwnedCombatExperience? credit = null, ExperienceContinuationExecution.StepPlan? step = null,
        WorldPacketHandlers.CompletedContinuationQuery? query = null, ExperienceContinuationExecution? clear = null,
        WorldPacketHandlers.OwnedContinuationQueryMutation? mutation = null)
    {
        if ((credit is null ? 0 : 1) + (step is null ? 0 : 1) + (query is null ? 0 : 1) + (clear is null ? 0 : 1) + (mutation is null ? 0 : 1) != 1)
            throw new InvalidOperationException("experienceContinuationFilePurposeInvalid");
        string expectedPath = Path.Combine(binding.UsersRoot, expected.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini");
        if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(expectedPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("experienceContinuationFilePathInvalid");
        var permit = new ContinuationFilePermit();
        ContinuationFilePermits.Add(permit, new(store, context, binding, expected, path, before.ToArray(), after.ToArray(),
            credit, step, query, clear, mutation));
        return permit;
    }

    internal ContinuationFilePermit IssueContinuationQueryFilePermit(WorldPacketHandlers.OwnedContinuationQueryMutation mutation,
        LegacyOrdinarySpellBinding binding, WorldSession expected, string path, byte[] before, byte[] after)
    {
        if (!ReferenceEquals(binding.Store, this) || !ReferenceEquals(mutation.Expected, expected) ||
            !ReferenceEquals(mutation.Context.Connection, binding.Connection) || !mutation.IsConsumedCurrentPlain(binding))
            throw new InvalidOperationException("experienceContinuationQueryFilePurposeInvalid");
        return IssueContinuationFilePermit(this, mutation.Context, binding, expected, path, before, after, mutation: mutation);
    }

    internal sealed class ContinuationFilePermit
    {
        internal bool Consume(string path, byte[] before, byte[] after)
        {
            if (!ContinuationFilePermits.TryGetValue(this, out var source) ||
                !string.Equals(path, source.Path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !before.AsSpan().SequenceEqual(source.Before) || !after.AsSpan().SequenceEqual(source.After)) return false;
            var store = source.Store; var binding = source.Binding; var context = source.Context;
            if (!ReferenceEquals(binding.Store, store) || !ReferenceEquals(binding.Connection, context.Connection) ||
                store.framePersistenceOwner is not { } frame || !frame.IsActionLifecycleCurrent(store, context) ||
                context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) != true ||
                !store._connectionGates.TryGetValue(binding.Connection, out var gate) || !Monitor.IsEntered(gate) ||
                !IsCurrentThreadInsideAnyConnectionGate || !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) ||
                !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !store._sessions.TryGetValue(binding.Connection, out var holder) || !store.BindingIsCurrentPlain(binding, holder, source.Expected)) return false;
            bool actual = source.Credit is { } credit ? credit.IsCurrentPlain() :
                source.Step is { } step ? step.IsHeldCurrentPlain(before) :
                source.Query is { } query ? query.TryCurrent(out var current) && ReferenceEquals(current, source.Expected) :
                source.Clear is { } clear ? clear.IsHeldCurrentPlain(before) :
                source.Mutation is { } mutation && ReferenceEquals(mutation.Context, context) &&
                    ReferenceEquals(mutation.Expected, source.Expected) && mutation.IsConsumedCurrentPlain(binding);
            if (!actual) return false;
            try
            {
                uint uid = checked((uint)source.Expected.Character.Uid);
                var oldState = LegacyExperienceContinuationCodec.ReadCharacterImage(before, source.Expected.AccountId, uid);
                var newState = LegacyExperienceContinuationCodec.ReadCharacterImage(after, source.Expected.AccountId, uid);
                LegacyExperienceContinuationCodec.ValidateTransition(oldState, newState);
                if (source.Credit is not null && (oldState is not null || newState is null) ||
                    source.Step is not null && (oldState?.Phase != LegacyExperienceContinuationPhase.Ready || newState?.Phase != LegacyExperienceContinuationPhase.QueryPending) ||
                    source.Query is not null && (oldState is null || newState is null || !source.Query.CoversPending(oldState)) ||
                    source.Clear is not null && (oldState?.Phase != LegacyExperienceContinuationPhase.Ready || newState is not null) ||
                    source.Mutation is not null && (oldState is null || !Equals(oldState, newState) || !Equals(oldState, source.Mutation.Pending))) return false;
                return ContinuationFilePermits.Remove(this);
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
            { return false; }
        }
    }
}
