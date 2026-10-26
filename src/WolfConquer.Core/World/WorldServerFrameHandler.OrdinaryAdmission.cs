using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly LegacyOrdinaryAdmissionUidOwner? _ordinaryAdmissionUids;

    internal WorldServerFrameHandler(CharacterSnapshotLoader loader, AccountHandoffService? handoff,
        WorldDataCatalog? worldData, LegacyOrdinaryAdmissionUidOwner ordinaryAdmissionUids,
        TextWriter? log = null, ConquerProtocolProfile protocolProfile = ConquerProtocolProfile.VisibleDx8D13)
        : this(loader, handoff, worldData, LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: protocolProfile)
    {
        if (ordinaryAdmissionUids is null || !ordinaryAdmissionUids.IsCurrent(LegacyOrdinaryAdmissionRoots.FromLoader(loader)))
            throw new ArgumentException("Ordinary admission UID owner differs from the explicit storage domain.");
        _ordinaryAdmissionUids = ordinaryAdmissionUids;
    }

    internal CharacterSnapshot ProjectOrdinaryStartupTitle(CharacterSnapshot snapshot) => WithLegacyClassPkWarStartupTitle(snapshot);

    private bool HasOrdinaryUidNamespace()
    {
        if (_ordinaryAdmissionUids is not null) return true;
        return _snapshotLoader is { } loader && LegacyOrdinaryAdmissionReconciliation.NamespaceMayExist(loader);
    }

    private async ValueTask StartLegacyOrdinaryExistingCharacterAsync(ConquerTcpConnection connection, long accountId,
        CharacterSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_snapshotLoader is not { } loader) return;
        if (!LegacyOrdinaryAdmissionPlan.TryPrepare(this, connection, accountId, snapshot.Name, snapshot, loader,
                _ordinaryAdmissionUids, _tradeRecovery, _runtimeUpdateLoop.TimeProvider ?? TimeProvider.System,
                out var plan, out var reason) || plan is null ||
            !_sessions.TryCommitOrdinaryAdmission(connection, plan, out var admitted, out reason) || admitted is null)
        {
            await _log.WriteLineAsync($"WorldServer skipped legacy accepted-login local prefix/admission key={accountId} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }
        // Only committed durable files + real current Store/native/PIN ownership
        // precede AnswerOk. A later network failure does not undo those files.
        await _log.WriteLineAsync($"WorldServer ordinary admission committed key={accountId}.").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_sessions.TryGet(connection, out var current) || !ReferenceEquals(current, admitted) ||
            !plan.IsPublishedNativeOwnerCurrent())
        {
            await _log.WriteLineAsync($"WorldServer ordinary admission committed but its publication owner changed key={accountId}.")
                .ConfigureAwait(false);
            return;
        }
        LegacySpellPublicationOwner? loginSpells = null;
        if (plan.LoginSpellPublication.Frames.Length != 0 &&
            !LegacySpellPublicationOwner.TryBind(_sessions, _tradeStates, connection, admitted,
                plan.LoginSpellPublication, out loginSpells)) return;
        await connection.SendAsync(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        if (!await PublishLegacyAdmittedSessionStartupAsync(connection, admitted, cancellationToken,
                (session, token) => SendLegacyWorldLoginDelayedStartupReviewsAsync(connection, session.Character, token),
                connection.NativeGameClient, loginSpells).ConfigureAwait(false))
        {
            await _log.WriteLineAsync($"WorldServer ordinary admission committed but startup delivery lost its owner key={accountId}.")
                .ConfigureAwait(false);
            return;
        }
        await _log.WriteLineAsync($"WorldServer legacy MsgLoginGame Success key={accountId} uid={admitted.Character.Uid} map={admitted.Character.MapId}.")
            .ConfigureAwait(false);
    }
}
