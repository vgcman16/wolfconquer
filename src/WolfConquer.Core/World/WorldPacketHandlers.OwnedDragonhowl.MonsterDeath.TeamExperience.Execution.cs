using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record DragonhowlExperienceSource(WorldSessionStore Store,
        WorldSessionStore.OwnedDragonhowlExperienceAward Award);
    private static readonly ConditionalWeakTable<OwnedCombatExperience, DragonhowlExperienceSource> DragonhowlExperienceSources = new();

    private static async ValueTask<bool> PublishDragonhowlTeamExperienceAsync(OwnedDragonhowlPreparation plan,
        WorldSessionStore.OwnedDragonhowlTeamExperience team, CancellationToken cancellationToken)
    {
        var store = plan.Store;
        if (!store.TryBeginDragonhowlTeamExperience(plan, team, out var awards)) return false;
        foreach (var award in awards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!store.TryStartDragonhowlExperienceAward(plan, award, out var context, out var before, out var gain, out var roster))
                return false;
            // This is the existing real private XP issuer. The registered kill
            // and exact retained recipient/amount are required before issuance.
            var purpose = IssueCombatExperience(context, context.Connection, before, gain, roster);
            if (!store.BindDragonhowlExperiencePurpose(award, context, before, gain, purpose)) return false;
            DragonhowlExperienceSources.Add(purpose, new(store, award));
            if (before.Character.Level < 140 && context.WorldData!.LevelExperienceByTypeAndLevel.TryGetValue(
                (0, checked((int)before.Character.Level)), out var threshold) && threshold.Experience > 0 &&
                checked((ulong)before.Character.Experience + gain) >= checked((ulong)threshold.Experience))
            {
                if (!store.TryCreditExperienceContinuation(context, context.Connection, before, gain, purpose,
                    out var credited, out _, out var credit, out var reason) || credited is null || credit is null)
                    throw new InvalidOperationException("ownedDragonhowlExperienceCreditRefused:" + reason);
                var actualContext = context with { Session = credited };
                WorldSessionStore.ExperienceContinuationExecution? execution;
                try
                {
                    if (!WorldSessionStore.ExperienceContinuationExecution.TryBeginCredit(store, actualContext, credit, out execution) ||
                        execution is null || !store.BindDragonhowlExperienceContinuation(award, execution))
                        throw new InvalidOperationException("ownedDragonhowlExperienceCreditOwnerChanged");
                }
                catch (Exception error) { credit.FenceFailure(error); throw; }
                var completed = await RunExperienceContinuationWithReceiptAsync(actualContext, execution,
                    cancellationToken, award).ConfigureAwait(false);
                if (!store.CompleteDragonhowlExperienceAward(award, completed)) return false;
            }
            else
            {
                if (!store.TryCommitCombatExperience(context, context.Connection, before, gain, purpose,
                    out var published, out _, out var receipt, out var frames, out var query, out var reason) ||
                    published is null || receipt is null || query is not null)
                    throw new InvalidOperationException("ownedDragonhowlExperienceScalarRefused:" + reason);
                await SendCommittedCombatExperienceAsync(context.Connection, published, receipt, frames, cancellationToken).ConfigureAwait(false);
                if (!store.CompleteDragonhowlExperienceAward(award, null)) return false;
            }
        }
        return store.IsDragonhowlTeamExperienceComplete(plan, team);
    }

    // Called only inside the existing real XP writer after durable/native/
    // holder publication and before observers. No callback grants acceptance.
    internal static void ConfirmDragonhowlExperiencePublication(WorldSessionStore store, OwnedCombatExperience purpose,
        WorldSession before, WorldSession after, WorldSessionStore.ActionPublication receipt, bool continuationCredit)
    {
        if (!DragonhowlExperienceSources.TryGetValue(purpose, out var source)) return;
        if (!ReferenceEquals(source.Store, store)) throw new InvalidOperationException("ownedDragonhowlExperienceStoreChanged");
        store.ConfirmDragonhowlExperiencePublication(source.Award, purpose, before, after, receipt, continuationCredit);
    }

    internal static bool TryEnterDragonhowlExperienceWriter(WorldSessionStore store, OwnedCombatExperience purpose,
        WorldPacketHandlerContext context, WorldSession expected, out IDisposable? scope)
    {
        scope = null;
        return !DragonhowlExperienceSources.TryGetValue(purpose, out var source) ||
            ReferenceEquals(source.Store, store) &&
            store.TryEnterDragonhowlExperienceWriter(source.Award, purpose, context, expected, out scope);
    }
}
