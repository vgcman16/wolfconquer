using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Explicit local host API. No game packet, remote authority, registry open,
    // arbitrary path or caller-provided item UID can invoke this capability.
    public async ValueTask<LegacyOperatorGearMailResult> DeliverOperatorTestGearMailAsync(
        LegacyOwnedAdmissionRuntime owner, LegacyOperatorGearMailRequest request,
        CancellationToken cancellationToken = default)
    {
        var operation = request?.OperationId ?? Guid.Empty;
        if (owner is null || !ReferenceEquals(owner, _ownedAdmissionRuntime) || _mailboxes is null ||
            _snapshotLoader is null || _worldData is null || _protocolProfile != ConquerProtocolProfile.VisibleDx8D13 ||
            !ReferenceEquals(_tradeRecovery, owner.Recovery))
            return OperatorMailResult(operation, LegacyOperatorGearMailStatus.Rejected, "operatorMailHostOwnerUnavailable");
        if (!TryPrepareOperatorMailRequest(request!, out var prepared, out var reason))
            return OperatorMailResult(operation, LegacyOperatorGearMailStatus.Rejected, reason);

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.ShutdownToken);
        using var actor = owner.EnterWorker();
        OperatorMailPlan? delivery = null;
        LegacyOperatorGearMailResult outcome;
        using (await _worldOperations.EnterExclusiveAsync(lifetime.Token).ConfigureAwait(false))
        lock (_tradeStates.SyncRoot)
        lock (_mailboxes.SyncRoot)
        {
            lifetime.Token.ThrowIfCancellationRequested();
            outcome = PrepareAndCommitOperatorMail(owner, prepared!, lifetime.Token, out delivery);
        }
        if (delivery is null) return outcome;
        return await NotifyOperatorGearMailAsync(owner, delivery, lifetime.Token).ConfigureAwait(false);
    }

    private LegacyOperatorGearMailResult PrepareAndCommitOperatorMail(LegacyOwnedAdmissionRuntime runtime,
        OperatorMailRequest request, CancellationToken cancellationToken, out OperatorMailPlan? delivery)
    {
        delivery = null;
        OperatorMailPlan? plan = null;
        var outcome = OperatorMailResult(request.Request.OperationId, LegacyOperatorGearMailStatus.Rejected,
            "operatorMailOwnerChanged");
        try
        {
            if (!TryCaptureOperatorMailOwner(runtime, request, out var owner, out var reason))
                return OperatorMailResult(request.Request.OperationId, LegacyOperatorGearMailStatus.Rejected, reason);
            // Actual Store gate precedes all baseline file locks and UID issuance.
            _sessions.WithCurrentNativeClientIdentity(owner!.Connection, owner.Expected.Character.Uid, identity =>
            {
                if (!ReferenceEquals(identity, owner.Identity)) return;
                outcome = PrepareOperatorMailPlan(runtime, request, owner, cancellationToken, out plan);
            });
            if (plan is null) return outcome;
            return CommitOperatorMail(runtime, plan, cancellationToken, out delivery);
        }
        catch (Exception error) when (IsOperatorMailPreparationFailure(error))
        {
            return OperatorMailResult(request.Request.OperationId, LegacyOperatorGearMailStatus.Rejected,
                "operatorMailPreparation:" + error.GetType().Name + ":reservationMayBeBurned");
        }
    }

    private static LegacyOperatorGearMailResult OperatorMailResult(Guid operation,
        LegacyOperatorGearMailStatus status, string reason, uint[]? mails = null, uint[]? items = null) =>
        new(operation, status, string.IsNullOrWhiteSpace(reason) ? "operatorMailRequiredStateUnavailable" : reason,
            mails ?? [], items ?? []);

    private static bool IsOperatorMailPreparationFailure(Exception error) => error is IOException or InvalidDataException or
        UnauthorizedAccessException or ArgumentException or InvalidOperationException or OverflowException or
        FormatException or IndexOutOfRangeException or NotSupportedException or System.Security.SecurityException;
}
