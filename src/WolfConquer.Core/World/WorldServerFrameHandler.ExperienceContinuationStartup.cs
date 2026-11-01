using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using System.Runtime.ExceptionServices;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Pending-login entry: unlike normal duplicate handling, this
    // refuses a competing live owner and never cancels escrow or disconnects.
    // The caller must already own the real login operation and admission gate.
    private bool TryPreparePendingExperienceAdmission(ConquerTcpConnection connection, long account,
        CharacterSnapshot snapshot, LegacyOrdinaryAdmissionPlan.ExperienceBeforeNativeEvidence evidence,
        out LegacyOrdinaryAdmissionPlan? plan, out string reason)
    {
        plan = null; reason = "experienceContinuationPendingOwnerUnavailable";
        if (!ordinaryLoginOperations.TryGetValue(connection, out var login) || !login.IsCurrent(this, connection) ||
            evidence.Pending is null || !evidence.IsCurrentPlain() || _snapshotLoader is not { } loader ||
            _tradeRecovery.IsBlocked || _sessions.Snapshot().Any(row => row.Session.AccountId == account ||
                row.Session.Character.Uid == snapshot.Uid)) return false;
        if (_mailboxPackages is { } packages &&
            (!packages.HasPendingAccountWork(account, out var pending, out reason) || pending)) return false;
        return LegacyOrdinaryAdmissionPlan.TryPrepare(this, connection, account, snapshot.Name, snapshot, loader,
            _ordinaryAdmissionUids, _tradeRecovery, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System,
            out plan, out reason, evidence);
    }

    internal bool TryPrepareDetachedInitialQueryData(WorldSession candidate,
        out WorldPacketHandlers.InitialWorldEntryQueryData? proof) =>
        WorldPacketHandlers.InitialWorldEntryQueryData.TryPrepare(candidate, _worldData,
            _runtimeUpdateLoop.LegacyClock ?? LegacySignedMillisecondClock.System, out proof);

    internal bool DetachedInitialQueryDataIsCurrent(WorldPacketHandlers.InitialWorldEntryQueryData proof,
        WorldSession candidate, LegacySpellPublicationPlan loginSpells) =>
        proof.IsCurrentFor(candidate, _worldData) && proof.PreservesLoginPublication(loginSpells);

    private bool TryPreparePublishedPendingExperienceStartup(ConquerTcpConnection connection, WorldSession admitted,
        LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin origin,
        LegacySpellPublicationOwner? loginSpells, out PendingExperienceStartup? startup)
    {
        startup = null;
        if (!ReferenceEquals(origin.Admitted, admitted) ||
            !TryCreateOrdinaryContinuationLoginContext(connection, admitted, out var context) ||
            !_sessions.TryPrepareAdmissionContinuationQuery(context, origin, out var query) || query is null) return false;
        startup = new PendingExperienceStartup(this, context, query, loginSpells);
        return true;
    }

    // Draft route; qualification is still required. The same real outer login
    // operation spans admission, startup and continuation.
    private async ValueTask<bool> PublishPendingExperienceAdmissionAsync(ConquerTcpConnection connection,
        long account, LegacyOrdinaryAdmissionPlan plan, LegacyOrdinaryAdmissionPlan.ExperienceBeforeNativeEvidence evidence,
        CancellationToken token)
    {
        if (!_sessions.TryCommitOrdinaryAdmission(connection, plan, out var admitted, out _) || admitted is null) return false;
        LegacyOrdinaryAdmissionPlan.ExperienceAdmissionOrigin? origin = null;
        LegacyOrdinarySpellBinding? committedBinding = null;
        PendingExperienceStartup? startup = null;
        try
        {
            committedBinding = admitted.NativeGameClient?.OrdinarySpellBinding;
            origin = plan.CaptureExperienceAdmissionOrigin(evidence);
            if (origin is null) throw new InvalidOperationException("experienceContinuationAdmissionOriginUnavailable");
            LegacySpellPublicationOwner? loginSpells = null;
            if (plan.LoginSpellPublication.Frames.Length != 0 &&
                !LegacySpellPublicationOwner.TryBind(_sessions, _tradeStates, connection, admitted,
                    plan.LoginSpellPublication, out loginSpells))
                throw new InvalidOperationException("experienceContinuationLoginSpellOwnerUnavailable");
            if (!TryPreparePublishedPendingExperienceStartup(connection, admitted, origin, loginSpells, out startup) || startup is null)
                throw new InvalidOperationException("experienceContinuationStartupOwnerUnavailable");
            token.ThrowIfCancellationRequested();
            await connection.SendAsync(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), token).ConfigureAwait(false);
            if (!await PublishLegacyAdmittedSessionStartupAsync(connection, admitted, token,
                    (session, cancellation) => SendLegacyWorldLoginDelayedStartupReviewsAsync(connection, session.Character, cancellation),
                    connection.NativeGameClient, loginSpells, startup).ConfigureAwait(false))
                throw new InvalidOperationException("experienceContinuationStartupOrResumeUnproven");
            await _log.WriteLineAsync($"WorldServer legacy MsgLoginGame Success key={account} uid={admitted.Character.Uid} map={admitted.Character.MapId}.")
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception primary)
        {
            // An actual invocation or completed predecessor fences its own
            // same binding even if publication preceded an observer failure.
            startup?.TryFenceFailure(primary);
            var errors = new List<Exception> { primary };
            try
            {
                bool held = origin is not null && _sessions.TryHoldPendingExperienceAdmission(origin);
                if (!held && committedBinding is not null) _sessions.TryRetireUnprovenPendingAdmission(plan, admitted, committedBinding);
            }
            catch (Exception error) { errors.Add(error); }
            try { plan.Recovery.Block("experienceContinuationAdmissionStartupUnproven"); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(primary).Throw();
            throw new AggregateException("Pending admission startup and cleanup failed.", errors);
        }
    }

    // Carries only privately issued query completions. It cannot be made from
    // a saved marker or an independently refreshed session.
    private sealed class PendingExperienceStartup
    {
        private readonly WorldServerFrameHandler frame;
        private readonly WorldPacketHandlerContext context;
        private readonly WorldPacketHandlers.PreparedAdmissionContinuationQuery first;
        private WorldPacketHandlers.CompletedAdmissionContinuationQuery? completed;
        private WorldPacketHandlers.PreparedAdmissionContinuationQuery? activeQuery;
        private bool startupComplete;
        internal LegacySpellPublicationOwner? LoginSpells { get; private set; }

        internal PendingExperienceStartup(WorldServerFrameHandler frame, WorldPacketHandlerContext context,
            WorldPacketHandlers.PreparedAdmissionContinuationQuery first, LegacySpellPublicationOwner? loginSpells)
        { this.frame = frame; this.context = context; this.first = first; activeQuery = first; LoginSpells = loginSpells; }

        internal async ValueTask<WorldSession?> RunFirstAsync(CancellationToken token)
        {
            if (completed is not null) return null;
            completed = await first.RunAsync(token).ConfigureAwait(false);
            return AdvanceLoginSpells() && completed is not null && completed.TryCurrent(out var session) ? session : null;
        }

        internal async ValueTask<WorldSession?> RunRevelatorAsync(CancellationToken token)
        {
            if (completed is not { } previous || !previous.TryCurrent(out var current) ||
                !frame._clientCompatibility.EnableNewerClientRevelatorStartupEffect ||
                !LegacyCombatGearPacket.TryCreateStartup(current.Character, current.CombatGear ?? CombatGearSnapshot.Empty, out _) ||
                !LegacyCombatGearStartupEffectPacket.TryCreatePirateRevelatorStartupFrames(current.Character, current.PlayerSpells, out _) ||
                !frame._sessions.TryPrepareAdmissionContinuationSuccessor(context with { Session = current }, previous, out var next) || next is null)
                return null;
            activeQuery = next;
            var nextCompletion = await next.RunAsync(token).ConfigureAwait(false);
            if (nextCompletion is null) return null;
            completed = nextCompletion;
            return AdvanceLoginSpells() && completed is not null && completed.TryCurrent(out var session) ? session : null;
        }

        private bool AdvanceLoginSpells()
        {
            if (completed is null) return false;
            if (LoginSpells is null) return true;
            if (!LoginSpells.TryAdvanceAfterAdmissionQuery(completed, out var successor) || successor is null) return false;
            LoginSpells = successor;
            return true;
        }

        internal async ValueTask<WorldSession?> ResumeAsync(CancellationToken token)
        {
            if (!startupComplete || completed is not { } receipt || !receipt.TryCurrent(out var current)) return null;
            return await WorldPacketHandlers.ResumeAfterAdmissionQueryAsync(context with { Session = current }, receipt, token)
                .ConfigureAwait(false);
        }

        internal void MarkStartupComplete() => startupComplete = completed is { } receipt && receipt.TryCurrent(out _);

        // Cleanup only. A private invocation owns any publication that may
        // have happened before an observer threw; an actual predecessor owns
        // a failed successor preparation. The caller handles pre-invocation
        // failure against the original admission origin.
        internal bool TryFenceFailure(Exception primary)
        {
            if (activeQuery is { } active && active.TryGetFailureOwner(out _, out _)) active.FenceFailure(primary);
            if (completed is { } receipt) receipt.FenceFailure(primary);
            return false;
        }
    }
}
