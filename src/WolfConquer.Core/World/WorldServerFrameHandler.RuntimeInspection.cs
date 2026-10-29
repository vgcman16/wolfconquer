using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool CanInspectOwnedRuntime(LegacyOwnedAdmissionRuntime owner, Guid domain) =>
        ReferenceEquals(owner, _ownedAdmissionRuntime) && ReferenceEquals(owner.Recovery, _tradeRecovery) &&
        _protocolProfile == ConquerProtocolProfile.VisibleDx8D13 &&
        owner.TryGetOperatorGearMailConfiguration(this, domain, out _, out _);

    public async ValueTask<LegacyRuntimeInspectionResult> InspectOwnedRuntimeAsync(LegacyOwnedAdmissionRuntime owner,
        LegacyRuntimeInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var query = request?.QueryId ?? Guid.Empty;
        LegacyRuntimeInspectionResult Refused(string reason) => new(1, query, LegacyRuntimeInspectionStatus.Rejected, reason, null);
        if (owner is null || request is null || request.SchemaVersion != 1 || query == Guid.Empty || request.ExpectedDomain == Guid.Empty ||
            request.AccountId == 0 || request.CharacterUid == 0 || !ReferenceEquals(owner, _ownedAdmissionRuntime) ||
            !ReferenceEquals(owner.Recovery, _tradeRecovery) || _protocolProfile != ConquerProtocolProfile.VisibleDx8D13)
            return Refused("runtimeInspectionRequestOrHostOwnerUnavailable");
        try
        {
            if (!owner.TryGetOperatorGearMailConfiguration(this, request.ExpectedDomain, out var routes, out _))
                return Refused("runtimeInspectionOwnedDomainUnavailable");
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.ShutdownToken);
            using var actor = owner.EnterWorker();
            // Read-only diagnostic lease. Exclusive blocks cooperating Frame/runtime writers;
            // it is NOT proof the exposed raw native Mana/item fields obey these gates.
            using (await _worldOperations.EnterExclusiveAsync(lifetime.Token).ConfigureAwait(false))
            lock (_tradeStates.SyncRoot)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                var active = _sessions.FindUniqueRuntimeInspectionSession(request.AccountId, request.CharacterUid);
                if (active is null) return Refused("runtimeInspectionUniqueCurrentSessionRequired");
                var client = active.Connection.NativeGameClient;
                if (client?.OrdinarySpellBinding is not { } binding || !ReferenceEquals(client, active.Session.NativeGameClient) ||
                    !ReferenceEquals(binding.Store, _sessions) || !ReferenceEquals(binding.Connection, active.Connection) ||
                    !ReferenceEquals(binding.Recovery, _tradeRecovery) || !active.Connection.HasSendRoute || !active.Connection.HasDisconnectRoute ||
                    _tradeStates.HasConnectionEnded(active.Connection) || !OperatorMailSameRoute(active.Session.UsersRoot, routes.UsersRoot) ||
                    !_sessions.TryReadRuntimeInspection(binding, active.Session, request.ExpectedDomain, out var snapshot))
                    return Refused("runtimeInspectionCurrentNativeGraphUnavailableOrChanged");
                lifetime.Token.ThrowIfCancellationRequested();
                if (!owner.TryGetOperatorGearMailConfiguration(this, request.ExpectedDomain, out var finalRoutes, out _) || finalRoutes != routes ||
                    !binding.PhysicalOwnerIsCurrentPlain() || !_sessions.IsBoundSpellOwnerCurrent(binding) ||
                    !_sessions.TryGet(active.Connection, out var current) || !ReferenceEquals(current, active.Session) ||
                    _tradeStates.HasConnectionEnded(active.Connection)) return Refused("runtimeInspectionOwnerRetiredDuringCopy");
                return new(1, query, LegacyRuntimeInspectionStatus.Observed, "boundedDiagnosticOnly", snapshot);
            }
        }
        catch (Exception error) when (IsOperatorMailPreparationFailure(error))
        { return Refused("runtimeInspectionUnavailable:" + error.GetType().Name); }
    }
}
