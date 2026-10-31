using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask StartWorldSessionAsync(
        ConquerTcpConnection connection,
        long accountId,
        string username,
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken,
        Func<WorldSession, CancellationToken, ValueTask>? afterStartupBeforeRuntime = null,
        LegacyNativeGameClientRuntime? nativeLoadedClient = null)
    {
        if (await TryStartMailboxDebtRecoverySessionAsync(connection, accountId, username, snapshot,
                nativeLoadedClient, cancellationToken).ConfigureAwait(false)) return;
        var nativeEquipment = BeginLegacyNativeEquipmentRawLoad(connection);
        var playerItems = _snapshotLoader?.LoadPlayerItemsByCharacterUid(snapshot.Uid,
            bytes => nativeEquipment?.CaptureRawLoad(bytes)) ?? PlayerItemSnapshot.Empty;
        var starterState = await EnsureLegacyFirstCharacterStarterStateAsync(
                snapshot,
                playerItems,
                cancellationToken)
            .ConfigureAwait(false);
        snapshot = starterState.Snapshot;
        playerItems = starterState.PlayerItems;
        if (starterState.Applied) nativeEquipment?.InvalidateRawLoadObservation("targetStarterReplacement");
        var playerSpells = _snapshotLoader?.LoadPlayerSpellsByCharacterUid(snapshot.Uid) ?? PlayerSpellSnapshot.Empty;
        var starterSpellState = await EnsureLegacyFirstCharacterStarterSpellsAsync(
                snapshot,
                playerItems,
                playerSpells,
                cancellationToken)
            .ConfigureAwait(false);
        playerSpells = starterSpellState.PlayerSpells;
        playerSpells = await EnsureLegacyPlayerSpellClassGatesAsync(
                snapshot,
                playerItems,
                playerSpells,
                cancellationToken)
            .ConfigureAwait(false);
        playerSpells = await EnsureLegacyClassOwnedSpellsAsync(
                snapshot,
                playerSpells,
                cancellationToken)
            .ConfigureAwait(false);
        await PersistLegacyFirstCharacterEmptyProficienciesAsync(snapshot, cancellationToken)
            .ConfigureAwait(false);
        var playerProficiencies = _snapshotLoader?.LoadPlayerProficienciesByCharacterUid(snapshot.Uid) ??
            PlayerProficiencySnapshot.Empty;
        var combatGear = _snapshotLoader?.LoadCombatGearByCharacterUid(snapshot.Uid) ?? CombatGearSnapshot.Empty;
        snapshot = await EnsureLegacyClassLevelStatStateAsync(
                snapshot,
                playerItems,
                combatGear,
                cancellationToken)
            .ConfigureAwait(false);
        snapshot = WithLegacyClassPkWarStartupTitle(snapshot);
        var session = new WorldSession(
            accountId,
            username,
            snapshot,
            playerItems,
            playerSpells,
            playerProficiencies,
            CombatGear: combatGear,
            UsersRoot: _snapshotLoader?.UsersRoot)
        {
            NativeGameClient = connection.NativeGameClient,
        };
        session = PrepareLegacySecondaryPasswordSession(connection, session);
        if (_tradeRecovery.IsBlocked) return;
        _sessions.Set(connection, session);
        await PublishLegacyAdmittedSessionStartupAsync(connection, session, cancellationToken,
            afterStartupBeforeRuntime, nativeLoadedClient).ConfigureAwait(false);
    }


    private async ValueTask<bool> PublishLegacyAdmittedSessionStartupAsync(
        ConquerTcpConnection connection, WorldSession session, CancellationToken cancellationToken,
        Func<WorldSession, CancellationToken, ValueTask>? afterStartupBeforeRuntime = null,
        LegacyNativeGameClientRuntime? nativeLoadedClient = null,
        LegacySpellPublicationOwner? loginSpells = null)
    {
        var snapshot = session.Character;
        await LogLegacySessionSidecarStatusAsync(session).ConfigureAwait(false);
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, snapshot.Uid, out session)) return false;
        await SendLegacyWorldEntryStartupAsync(
                connection,
                session.Character,
                session.PlayerItems,
                session.PlayerSpells,
                session.CombatGear ?? CombatGearSnapshot.Empty,
                cancellationToken,
                nativeLoadedClient: nativeLoadedClient,
                loginSpells: loginSpells)
            .ConfigureAwait(false);
        if (loginSpells is not null && !loginSpells.IsCurrent()) return false;
        var ownerUid = snapshot.Uid;
        if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out session)) return false;
        if (afterStartupBeforeRuntime is not null)
        {
            await afterStartupBeforeRuntime(session, cancellationToken)
                .ConfigureAwait(false);
            if (!TryRefreshLegacyPhysicalStatusRuntimeSubject(connection, ownerUid, out session)) return false;
        }

        if (loginSpells is not null && !loginSpells.IsCurrent()) return false;
        RegisterLegacyStaminaReadiness(connection, session);
        StartLegacyRuntimeUpdateLoop(
            connection,
            session.Character,
            session.PlayerItems,
            session.PlayerSpells,
            cancellationToken);
        return true;
    }
}
