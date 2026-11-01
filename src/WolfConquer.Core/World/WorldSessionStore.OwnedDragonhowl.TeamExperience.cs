using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal sealed class OwnedDragonhowlTeamExperience { }
    internal sealed class OwnedDragonhowlExperienceAward { }

    private sealed class DragonhowlTeamExperienceState(DragonhowlMonsterDeathState death,
        TeamSessionStateStore.DragonhowlExperienceRoster membership,
        WorldPacketHandlers.DragonhowlMonsterTeamExperienceData? data)
    {
        internal readonly DragonhowlMonsterDeathState Death = death;
        internal readonly TeamSessionStateStore.DragonhowlExperienceRoster Membership = membership;
        internal readonly WorldPacketHandlers.DragonhowlMonsterTeamExperienceData? Data = data;
        internal readonly List<OwnedDragonhowlExperienceAward> Awards = [];
        internal int Next;
        internal bool Started, Complete;
    }

    private sealed class DragonhowlExperienceAwardState(DragonhowlTeamExperienceState team,
        WorldPacketHandlers.DragonhowlTeamExperienceRecipient data)
    {
        internal readonly DragonhowlTeamExperienceState Team = team;
        internal readonly WorldPacketHandlers.DragonhowlTeamExperienceRecipient Data = data;
        internal WorldSession? Before, Published;
        internal WorldPacketHandlerContext? Context;
        internal WorldPacketHandlers.OwnedCombatExperience? Purpose;
        internal ActionPublication? ScalarReceipt;
        internal ExperienceContinuationExecution? Continuation;
        internal WorldPacketHandlers.CompletedExperienceContinuation? Terminal;
        internal bool Started, CreditPublished, Complete;
    }

    private readonly ConditionalWeakTable<OwnedDragonhowlTeamExperience, DragonhowlTeamExperienceState> dragonhowlTeamExperience = new();
    private readonly ConditionalWeakTable<OwnedDragonhowlExperienceAward, DragonhowlExperienceAwardState> dragonhowlExperienceAwards = new();
    private readonly ConditionalWeakTable<DragonhowlMonsterDeathState, OwnedDragonhowlTeamExperience> dragonhowlDeathExperience = new();

    internal bool TryPrepareDragonhowlTeamExperience(OwnedDragonhowlOwner purpose, OwnedDragonhowlMonsterDeath death, WorldSession calculatedKiller,
        IReadOnlyDictionary<ConquerTcpConnection, WorldSession> calculated,
        out OwnedDragonhowlTeamExperience? prepared,
        out IReadOnlyList<WorldPacketHandlers.DragonhowlTeamExperienceRecipient> recipients)
    {
        prepared = null; recipients = [];
        if (!dragonhowlOwners.TryGetValue(purpose, out var issuingOwner) ||
            !dragonhowlMonsterDeaths.TryGetValue(death, out var source) || !ReferenceEquals(source.Source.Owner, issuingOwner) ||
            source.Active || source.Steps.Count != 2 ||
            source.Source.Owner.Plan is not null || !DragonhowlHeldCurrent(source.Source.Owner) ||
            source.Source.Owner.Context.TeamStates is not { } teams || !source.Source.BeforeTargetCurrent() ||
            dragonhowlDeathExperience.TryGetValue(source, out _) ||
            !dragonhowlDeathCps.TryGetValue(source.Steps[^1], out var tail) || !ReferenceEquals(tail.Data.After, calculatedKiller)) return false;
        var owner = source.Source.Owner;
        var killer = owner.Bindings[owner.Context.Connection];
        var membership = teams.CaptureDragonhowlExperienceRoster(this, purpose, killer);
        if (membership is null) return false;
        WorldPacketHandlers.DragonhowlMonsterTeamExperienceData? data = null;
        if (membership.Roster is { } roster)
        {
            var members = new List<WorldPacketHandlers.DragonhowlTeamExperienceMember>();
            foreach (var row in roster.Members)
            {
                if (!owner.Current.TryGetValue(row.Connection, out var original) ||
                    !owner.Bindings.TryGetValue(row.Connection, out var binding) || binding.Uid != row.Uid ||
                    !owner.Frame.TryPrepareActionContext(this, owner.Context with { Connection = row.Connection, Session = original },
                        original, out var context)) return false;
                var projected = ReferenceEquals(row.Connection, killer.Connection) ? calculatedKiller :
                    calculated.TryGetValue(row.Connection, out var value) ? value : original;
                owner.PlannedAwards.TryGetValue(row.Connection, out var previousAward);
                members.Add(new(context, binding, projected, previousAward?.Data.Levels));
            }
            if (!WorldPacketHandlers.DragonhowlMonsterTeamExperienceData.TryPrepare(owner.Context, killer,
                calculatedKiller, source.Source.Monster!, roster, members.ToArray(), out data) || data is null) return false;
        }
        var state = new DragonhowlTeamExperienceState(source, membership, data);
        foreach (var row in data?.Recipients ?? [])
        {
            if (row.Gain == 0) continue;
            var award = new OwnedDragonhowlExperienceAward();
            var awardState = new DragonhowlExperienceAwardState(state, row);
            dragonhowlExperienceAwards.Add(award, awardState); state.Awards.Add(award);
            owner.PlannedAwards[row.Member.Binding.Connection] = awardState;
        }
        var token = new OwnedDragonhowlTeamExperience();
        dragonhowlTeamExperience.Add(token, state); dragonhowlDeathExperience.Add(source, token);
        prepared = token; recipients = data?.Recipients ?? [];
        return membership.IsCurrentPlain() && (data is null || data.IsCurrentPlain());
    }

    internal bool IsDragonhowlExperienceKillerHeld(OwnedDragonhowlOwner purpose, TeamSessionStateStore teams,
        LegacyOrdinarySpellBinding killer) => dragonhowlOwners.TryGetValue(purpose, out var owner) &&
        !owner.Failed && !owner.Completed && ReferenceEquals(owner.Context.TeamStates, teams) &&
        ReferenceEquals(owner.Context.Connection, killer.Connection) &&
        owner.Bindings.TryGetValue(killer.Connection, out var actual) && ReferenceEquals(actual, killer) && DragonhowlHeldCurrent(owner);

    internal bool IsPreparedDragonhowlTeamExperienceCurrent(OwnedDragonhowlTeamExperience token) =>
        dragonhowlTeamExperience.TryGetValue(token, out var state) && !state.Started && !state.Complete &&
        !state.Death.Active && !state.Death.Source.Owner.Started && DragonhowlHeldCurrent(state.Death.Source.Owner) &&
        state.Death.Source.BeforeTargetCurrent() && state.Membership.IsCurrentPlain() &&
        (state.Data is null || state.Data.IsCurrentPlain());

    internal bool DragonhowlTeamExperienceInputsCurrent(OwnedDragonhowlTeamExperience token) =>
        dragonhowlTeamExperience.TryGetValue(token, out var state) && !state.Death.Source.Owner.Failed &&
        state.Membership.IsCurrentPlain() && (state.Data is null || state.Data.RecordedInputsAreCurrent());

    internal bool IsDragonhowlRewardParticipant(OwnedDragonhowlOwner owner, OwnedDragonhowlTeamExperience token,
        ConquerTcpConnection connection) => dragonhowlOwners.TryGetValue(owner, out var issuing) &&
        dragonhowlTeamExperience.TryGetValue(token, out var state) && ReferenceEquals(state.Death.Source.Owner, issuing) &&
        state.Membership.Roster?.Members.Any(row => ReferenceEquals(row.Connection, connection)) == true;

    internal bool TryBeginDragonhowlTeamExperience(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlTeamExperience token, out OwnedDragonhowlExperienceAward[] awards)
    {
        awards = [];
        if (!dragonhowlTeamExperience.TryGetValue(token, out var state)) return false;
        var owner = state.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (state.Started || state.Complete || !ReferenceEquals(owner.Plan, plan) ||
                !DragonhowlMonsterRewardsCompleteHeld(plan, state.Death) || !state.Membership.IsCurrentPlain()) return false;
            awards = state.Awards.ToArray(); state.Started = true;
            if (awards.Length == 0) state.Complete = true;
            return true;
        }
    }

    internal bool TryStartDragonhowlExperienceAward(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlExperienceAward token, out WorldPacketHandlerContext context, out WorldSession before,
        out ulong gain, out TeamSessionStateStore.CombatExperienceRoster? roster)
    {
        context = null!; before = null!; gain = 0; roster = null;
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state)) return false;
        var owner = state.Team.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            var member = state.Data.Member;
            if (state.Started || !DragonhowlExperienceTurnCurrent(plan, token, state) ||
                !owner.Current.TryGetValue(member.Binding.Connection, out var current) ||
                !DragonhowlExperienceBeforeMatches(owner, member.Binding, current, member.CalculatedBefore) ||
                !owner.Frame.TryPrepareActionContext(this, member.Context with { Session = current }, current, out context)) return false;
            state.Started = true; state.Before = current; state.Context = context;
            before = current; gain = state.Data.Gain; roster = state.Team.Data!.Roster;
            return true;
        }
    }

    internal bool BindDragonhowlExperiencePurpose(OwnedDragonhowlExperienceAward token,
        WorldPacketHandlerContext context, WorldSession before, ulong gain,
        WorldPacketHandlers.OwnedCombatExperience purpose)
    {
        if (!dragonhowlExperienceAwards.TryGetValue(token, out var state)) return false;
        var owner = state.Team.Death.Source.Owner;
        lock (owner.Frame.RequireActionLifecycleGate(this))
        {
            using var scope = new DragonhowlOwnerScope(this, DragonhowlCurrentRows(owner));
            if (!state.Started || state.Purpose is not null || !ReferenceEquals(state.Context, context) ||
                !ReferenceEquals(state.Before, before) || state.Data.Gain != gain || owner.Plan is not { } plan ||
                !DragonhowlExperienceTurnCurrent(plan, token, state) ||
                !purpose.Covers(this, context, context.Connection, before, gain)) return false;
            state.Purpose = purpose;
            return true;
        }
    }

    private bool DragonhowlExperienceTurnCurrent(WorldPacketHandlers.OwnedDragonhowlPreparation plan,
        OwnedDragonhowlExperienceAward token, DragonhowlExperienceAwardState state) =>
        state.Team.Started && !state.Team.Complete && state.Team.Next < state.Team.Awards.Count &&
        ReferenceEquals(state.Team.Awards[state.Team.Next], token) &&
        DragonhowlMonsterRewardsCompleteHeld(plan, state.Team.Death) && state.Team.Membership.IsCurrentPlain();

    private static bool DragonhowlExperienceSessionMatches(WorldSession actual, WorldSession expected)
    {
        if ((actual with { Character = expected.Character }) != expected ||
            (actual.Character with { Attributes = expected.Character.Attributes,
                PaidNobility = expected.Character.PaidNobility }) != expected.Character) return false;
        var left = LegacyNativeAttributeMapProof.CaptureRaw(actual.Character.Attributes);
        var right = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.Attributes);
        var paidLeft = LegacyNativeAttributeMapProof.CaptureRaw(actual.Character.PaidNobility);
        var paidRight = LegacyNativeAttributeMapProof.CaptureRaw(expected.Character.PaidNobility);
        return ReferenceEquals(left.CapturedComparer, right.CapturedComparer) && left.Fields.SequenceEqual(right.Fields) &&
            ReferenceEquals(paidLeft.CapturedComparer, paidRight.CapturedComparer) && paidLeft.Fields.SequenceEqual(paidRight.Fields) &&
            left.IsCurrentPlain() && right.IsCurrentPlain() && paidLeft.IsCurrentPlain() && paidRight.IsCurrentPlain();
    }

    private bool DragonhowlExperienceBeforeMatches(DragonhowlOwnerState owner, LegacyOrdinarySpellBinding binding,
        WorldSession current, WorldSession calculated)
    {
        if (DragonhowlExperienceSessionMatches(current, calculated)) return true;
        // A previous monster's real award may have rebuilt immutable query
        // caches. Only its completed final-clear receipt can join those actual
        // cache identities to the exact next preflight projection.
        if (!owner.CompletedAwards.TryGetValue(binding.Connection, out var previous) || !previous.Complete ||
            !ReferenceEquals(previous.Published, current) || !ReferenceEquals(previous.Data.ProjectedAfter, calculated)) return false;
        if (previous.CreditPublished)
            return previous.Terminal is not null && previous.Data.Levels is { } levels &&
                previous.Terminal.MatchesCompletedReceiveData(binding, current, levels);
        return previous.Before is { } before && previous.ScalarReceipt is { } receipt &&
            receipt.TryCurrent(out var published) && ReferenceEquals(published, current) &&
            DragonhowlExperienceSessionMatches(current, before with { Character = calculated.Character });
    }
}
