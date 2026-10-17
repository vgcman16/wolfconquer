using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacySupermanStatusFlag = 0x12;
    private const int LegacyAccuracyStatusFlag = 0x07;
    private const int LegacyCycloneStatusFlag = 0x17;
    private const int LegacyOblivionStatusFlag = 0x70;
    private const int LegacyFatalStrikeStatusFlag = 0x2F;
    private const int LegacyChaintBoltStatusFlag = 0x5C;
    private const int LegacyCannonBarrageStatusFlag = 0x78;
    private const int LegacyBlackbeardsRageStatusFlag = 0x79;
    private const int LegacyBladeFlurryStatusFlag = 0x92;
    private const int LegacySuperCycloneStatusFlag = 0x96;
    private const int LegacyDragonCycloneStatusFlag = 0x9F;
    private const int LegacyOmnipotenceStatusFlag = 0xC0;
    private const int LegacyShurikenVortexStatusFlag = 0x2E;
    private const int LegacyManiacDanceStatusFlag = 0xB5;
    private const int LegacyThunderRampageStatusFlag = 0xEB;
    private const string RuntimeLegacySupermanExpiresAtUtcAttribute = "LegacySupermanExpiresAtUtc";
    private const string RuntimeLegacyCycloneExpiresAtUtcAttribute = "LegacyCycloneExpiresAtUtc";
    private const string RuntimeLegacyFatalStrikeExpiresAtUtcAttribute = "LegacyFatalStrikeExpiresAtUtc";
    private const string RuntimeLegacyChainBoltExpiresAtUtcAttribute = "LegacyChainBoltExpiresAtUtc";
    private const string RuntimeLegacyBladeFlurryExpiresAtUtcAttribute = "LegacyBladeFlurryExpiresAtUtc";
    private const string RuntimeLegacyCannonBarrageExpiresAtUtcAttribute = "LegacyCannonBarrageExpiresAtUtc";
    private const string RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute = "LegacyBlackbeardsRageExpiresAtUtc";
    private const string RuntimeLegacySuperCycloneExpiresAtUtcAttribute = "LegacySuperCycloneExpiresAtUtc";
    private const string RuntimeLegacyDragonCycloneExpiresAtUtcAttribute = "LegacyDragonCycloneExpiresAtUtc";
    private const string RuntimeLegacyOmnipotenceExpiresAtUtcAttribute = "LegacyOmnipotenceExpiresAtUtc";
    private const string RuntimeLegacyShurikenVortexExpiresAtUtcAttribute = "LegacyShurikenVortexExpiresAtUtc";
    private const string RuntimeLegacyManiacDanceExpiresAtUtcAttribute = "LegacyManiacDanceExpiresAtUtc";
    private const string RuntimeLegacyThunderRampageExpiresAtUtcAttribute = "LegacyThunderRampageExpiresAtUtc";

    private async ValueTask SendDueLegacyXpSkillExpirationsAsync(
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
                !TryExpireLegacyXpSkillFlags(
                    currentSession.Character,
                    now,
                    out var expiredCharacter,
                    out var removedVisibleFlag))
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

            playerCount++;
        }

        if (playerCount > 0)
        {
            await _log.WriteLineAsync(
                    $"WorldServer expired legacy XP skill flags playerCount={playerCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryExpireLegacyXpSkillFlags(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out bool removedVisibleFlag)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        var changed = false;
        removedVisibleFlag = false;

        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyCycloneStatusFlag,
            RuntimeLegacyCycloneExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacySupermanStatusFlag,
            RuntimeLegacySupermanExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyOblivionStatusFlag,
            WorldPacketHandlers.RuntimeLegacyOblivionExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        if (TryExpireLegacyXpSkillFlag(
                attributes,
                LegacyAccuracyStatusFlag,
                WorldPacketHandlers.RuntimeLegacyAccuracyExpiresAtUtcAttribute,
                now,
                ref removedVisibleFlag))
        {
            attributes.Remove(WorldPacketHandlers.RuntimeLegacyAccuracyRemoveOnDeadAttribute);
            changed = true;
        }
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyFatalStrikeStatusFlag,
            RuntimeLegacyFatalStrikeExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyChaintBoltStatusFlag,
            RuntimeLegacyChainBoltExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyBladeFlurryStatusFlag,
            RuntimeLegacyBladeFlurryExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacySuperCycloneStatusFlag,
            RuntimeLegacySuperCycloneExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyDragonCycloneStatusFlag,
            RuntimeLegacyDragonCycloneExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyOmnipotenceStatusFlag,
            RuntimeLegacyOmnipotenceExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        if (TryExpireLegacyXpSkillFlag(
                attributes,
                LegacyShurikenVortexStatusFlag,
                RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
                now,
                ref removedVisibleFlag))
        {
            attributes.Remove(WorldPacketHandlers.RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute);
            changed = true;
        }
        if (TryExpireLegacyXpSkillFlag(
                attributes,
                LegacyManiacDanceStatusFlag,
                RuntimeLegacyManiacDanceExpiresAtUtcAttribute,
                now,
                ref removedVisibleFlag))
        {
            attributes.Remove(WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute);
            changed = true;
        }
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyCannonBarrageStatusFlag,
            RuntimeLegacyCannonBarrageExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyBlackbeardsRageStatusFlag,
            RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);
        changed |= TryExpireLegacyXpSkillFlag(
            attributes,
            LegacyThunderRampageStatusFlag,
            RuntimeLegacyThunderRampageExpiresAtUtcAttribute,
            now,
            ref removedVisibleFlag);

        updatedCharacter = changed
            ? character with { Attributes = attributes }
            : character;
        return changed;
    }

    private static bool TryExpireLegacyXpSkillFlag(
        Dictionary<string, string> attributes,
        int statusFlag,
        string expiresAtAttribute,
        DateTimeOffset now,
        ref bool removedVisibleFlag)
    {
        if (!attributes.TryGetValue(expiresAtAttribute, out var rawExpiresAt) ||
            !DateTimeOffset.TryParse(
                rawExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiresAt) ||
            expiresAt > now.ToUniversalTime())
        {
            return false;
        }

        attributes.Remove(expiresAtAttribute);
        if (RemoveLegacyStatusFlag(attributes, statusFlag))
        {
            removedVisibleFlag = true;
        }

        return true;
    }

    private static bool RemoveLegacyStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        if (flag == LegacyLightningShieldActivationLifetime.StatusFlag)
            LegacyLightningShieldActivationLifetime.Forget(attributes);
        if (flag == LegacyLightningShieldBaseLifetime.StatusFlag)
            LegacyLightningShieldBaseLifetime.Forget(attributes);
        LegacyArrowBladesFlagRegistration.Forget(attributes, flag);
        var key = attributes.ContainsKey("StatusFlags") ? "StatusFlags" : "BitVector";
        if (!attributes.TryGetValue(key, out var raw))
        {
            return false;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wordIndex >= parts.Length)
        {
            return false;
        }

        var words = new ulong[Math.Max(10, parts.Length)];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        var mask = 1UL << bitIndex;
        if ((words[wordIndex] & mask) == 0)
        {
            return false;
        }

        words[wordIndex] &= ~mask;
        attributes[key] = string.Join('/', words) + "/";
        return true;
    }
}
