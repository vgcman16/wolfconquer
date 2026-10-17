using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyHawksAmbitionStatusFlag = 0x100;
    private const int LegacyMudWallStatusFlag = 0x112;
    private const int LegacyBonePulseStatusFlag = 0x11F;

    private const string RuntimeLegacyHawksAmbitionExpiresAtUtcAttribute =
        "LegacyHawksAmbitionExpiresAtUtc";
    private const string RuntimeLegacyHawksAmbitionRemoveOnDeadAttribute =
        "LegacyHawksAmbitionRemoveOnDead";
    private const string RuntimeHawksAmbitionPowerAttribute = "HawksAmbitionPower";

    private const string RuntimeLegacyBonePulseExpiresAtUtcAttribute =
        "LegacyBonePulseExpiresAtUtc";
    private const string RuntimeBonePulseExpiresAtUtcAttribute = "BonePulseExpiresAtUtc";
    private const string RuntimeLegacyBonePulseRemoveOnDeadAttribute =
        "LegacyBonePulseRemoveOnDead";
    private const string RuntimeBonePulsePowerAttribute = "BonePulsePower";
    private const string RuntimeLegacyBonePulsePowerAttribute = "LegacyBonePulsePower";

    private const string RuntimeLegacyMudWallExpiresAtUtcAttribute = "LegacyMudWallExpiresAtUtc";
    private const string RuntimeLegacyMudWallRemoveOnDeadAttribute = "LegacyMudWallRemoveOnDead";

    private static readonly string[] RuntimeBonePulseExpiryAttributes =
    [
        RuntimeLegacyBonePulseExpiresAtUtcAttribute,
        RuntimeBonePulseExpiresAtUtcAttribute,
    ];

    private static readonly string[] RuntimeBonePulseAttributesToRemove =
    [
        RuntimeLegacyBonePulseExpiresAtUtcAttribute,
        RuntimeBonePulseExpiresAtUtcAttribute,
        RuntimeLegacyBonePulseRemoveOnDeadAttribute,
        RuntimeBonePulsePowerAttribute,
        RuntimeLegacyBonePulsePowerAttribute,
    ];

    private async ValueTask SendDueLegacyCombatGearAttachStatusExpirationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var playerCount = 0;
        var viewerCount = 0;
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null ||
                !TryExpireLegacyCombatGearAttachStatusFlags(
                    currentSession.Character,
                    now,
                    out var expiredCharacter,
                    out var removedVisibleFlag,
                    out var removedMudWallFlag))
            {
                continue;
            }

            var updatedSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, updatedSession);
            if (!removedVisibleFlag)
            {
                continue;
            }

            var uid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            var statusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    uid,
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        expiredCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();

            await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
            viewerCount++;

            foreach (var viewer in activeSessions)
            {
                if (ReferenceEquals(viewer.Connection, active.Connection) ||
                    viewer.Session.Character.Uid == expiredCharacter.Uid ||
                    !CanSeePlayer(viewer.Session.Character, expiredCharacter))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }

            if (removedMudWallFlag)
            {
                await SendLegacyMudWallExpiryEquipmentAppearanceRefreshAsync(
                        activeSessions,
                        active.Connection,
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer expired legacy HawksAmbition/BonePulse/MudWall flags " +
                    $"playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyCombatGearAttachStatusFlags(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag,
        out bool removedMudWallFlag)
    {
        var changed = false;
        removedVisibleFlag = false;
        removedMudWallFlag = false;
        updatedCharacter = character;

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyHawksAmbitionStatusFlag,
                RuntimeLegacyHawksAmbitionExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedHawksAmbitionFlag,
                RuntimeLegacyHawksAmbitionRemoveOnDeadAttribute,
                RuntimeHawksAmbitionPowerAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedHawksAmbitionFlag;
        }

        foreach (var expiresAtAttribute in RuntimeBonePulseExpiryAttributes)
        {
            if (!TryExpireLegacyTimedStatusFlag(
                    updatedCharacter,
                    now,
                    LegacyBonePulseStatusFlag,
                    expiresAtAttribute,
                    out updatedCharacter,
                    out var removedBonePulseFlag,
                    RuntimeBonePulseAttributesToRemove))
            {
                continue;
            }

            changed = true;
            removedVisibleFlag |= removedBonePulseFlag;
            break;
        }

        if (TryExpireLegacyTimedStatusFlag(
                updatedCharacter,
                now,
                LegacyMudWallStatusFlag,
                RuntimeLegacyMudWallExpiresAtUtcAttribute,
                out updatedCharacter,
                out var removedMudWallVisibleFlag,
                RuntimeLegacyMudWallRemoveOnDeadAttribute))
        {
            changed = true;
            removedVisibleFlag |= removedMudWallVisibleFlag;
            removedMudWallFlag = removedMudWallVisibleFlag;
        }

        return changed;
    }

    private async ValueTask SendLegacyMudWallExpiryEquipmentAppearanceRefreshAsync(
        IReadOnlyList<WorldActiveSession> activeSessions,
        ConquerTcpConnection ownerConnection,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var equipmentRefresh = LegacyShowEquipmentPacket.CreateEquipmentRefresh(
            session.PlayerItems,
            session.AlternateEquipmentActive);
        await ownerConnection.SendAsync(equipmentRefresh.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var currentHitpoints = CheckedUInt32(
            session.Character.MinHitPoints,
            nameof(session.Character.MinHitPoints));
        var maxHitpoints = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(
            session.Character,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            _worldData,
            session.AlternateEquipmentActive);
        var spawnFrame = LegacyPlayerSpawnPacket
            .FromSnapshot(
                session.Character,
                session.PlayerItems,
                alternateEquipmentActive: session.AlternateEquipmentActive,
                maxLife: maxHitpoints)
            .ToFrameBytes();

        if (_worldData is not null)
        {
            await ownerConnection.SendAsync(spawnFrame, cancellationToken).ConfigureAwait(false);
        }

        var visiblePeerCount = 0;
        foreach (var viewer in activeSessions)
        {
            if (ReferenceEquals(viewer.Connection, ownerConnection) ||
                viewer.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(viewer.Session.Character, session.Character))
            {
                continue;
            }

            await viewer.Connection.SendAsync(spawnFrame, cancellationToken).ConfigureAwait(false);
            visiblePeerCount++;
        }

        await _log.WriteLineAsync(
                "WorldServer sent legacy MudWall expiry QueryEquipment-equivalent appearance refresh " +
                $"uid={session.Character.Uid} alternate={equipmentRefresh.Alternante} " +
                $"visiblePeerCount={visiblePeerCount}.")
            .ConfigureAwait(false);
    }
}
