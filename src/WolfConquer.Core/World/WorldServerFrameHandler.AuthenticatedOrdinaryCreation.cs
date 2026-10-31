using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal LegacyJournalInterruption? AuthenticatedCreationInterruption { get; set; }
    internal LegacyAuthenticatedCreationInterruption AuthenticatedCreationProducerInterruption { get; set; }

    private async ValueTask HandleAuthenticatedOrdinaryCreationAsync(ConquerTcpConnection connection,
        PacketFrame frame, PendingLegacyCharacterCreation pending, CancellationToken cancellationToken)
    {
        if (_ownedAdmissionRuntime is not { } runtime || !pending.Authenticated ||
            !LegacyNewRolePacket.TryParse(frame, out var request) ||
            !TryNormalizeLegacyNewRoleName(request.Name, out var name) || !IsLegacyNewRoleBody(request.Body) ||
            !TryMapLegacyNewRoleClass(request.ClassSelection, out _))
        {
            await _log.WriteLineAsync("WorldServer authenticated NewRole request refused before creation.").ConfigureAwait(false);
            return;
        }
        CharacterSnapshot? character = null; var reason = "authenticatedCreationPermitRequired";
        bool created;
        // The issuer, every post-intent failure fence and disposal share this
        // actual Frame's existing trade/action serialization. No await is held.
        lock (_tradeStates.SyncRoot)
        {
            created = runtime.TryEnterAuthenticatedCreation(this, connection, name, out var lease, out reason) && lease is not null;
            if (created)
            {
                using var actualLease = lease!;
                try
                {
                    actualLease.RequireActualFrame(this);
                    var catalog = _worldData ?? throw new InvalidDataException("Authenticated creation needs its actual adopted world catalog.");
                    var projection = LegacyAuthenticatedCharacterCreationProjection.Capture(catalog, actualLease.AccountId,
                        name, request.Body, request.ClassSelection,
                        (_runtimeUpdateLoop.TimeProvider ?? TimeProvider.System).GetLocalNow().DateTime);
                    created = LegacyAuthenticatedOrdinaryCreationProducer.TryCommit(actualLease, projection,
                        AuthenticatedCreationInterruption, AuthenticatedCreationProducerInterruption,
                        cancellationToken, out character, out reason);
                }
                catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
                { created = false; reason = "authenticatedCreationPreparation:" + error.GetType().Name; }
            }
        }
        if (!created || character is null)
        {
            await _log.WriteLineAsync($"WorldServer authenticated NewRole refused uid={pending.CharacterUid} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }
        _pendingCreations.Remove(connection);
        // Creation completion/history is already durable. Ordinary admission
        // owns all native/Store publication and the existing startup packets.
        await StartLegacyOrdinaryExistingCharacterAsync(connection, character.Uid, character, cancellationToken)
            .ConfigureAwait(false);
    }
}
