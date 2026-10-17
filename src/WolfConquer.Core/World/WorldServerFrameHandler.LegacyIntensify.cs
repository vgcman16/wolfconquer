using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const int LegacyFocusedStatusFlag = 0x15;
    private static readonly TimeSpan LegacyIntensifyArmDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LegacyFocusedDuration = TimeSpan.FromSeconds(60);

    private async ValueTask SendDueLegacyIntensifyFocusUpdatesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = _sessions.Snapshot();
        if (activeSessions.Count == 0)
        {
            return;
        }

        var promotedCount = 0;
        var expiredCount = 0;
        var viewerCount = 0;
        foreach (var active in activeSessions)
        {
            if (!_sessions.TryGet(active.Connection, out var currentSession) ||
                currentSession is null)
            {
                continue;
            }

            if (TryPromoteLegacyIntensifyFocus(
                    currentSession.Character,
                    now,
                    out var promotedCharacter,
                    out var focusSpellId,
                    out var focusSpellLevel,
                    out var useSpellSoul))
            {
                var promotedSession = currentSession with { Character = promotedCharacter };
                _sessions.Set(active.Connection, promotedSession);
                currentSession = promotedSession;
                promotedCount++;

                var uid = CheckedUInt32(promotedCharacter.Uid, nameof(promotedCharacter.Uid));
                var animation = new LegacySpellAnimationPacket(
                    uid,
                    AttackedUid: uid,
                    X: 0,
                    Y: 0,
                    SpellId: focusSpellId,
                    SpellLevel: focusSpellLevel,
                    NextSpell: 0,
                    LevelHu: useSpellSoul,
                    SpellEffect: 0,
                    [new LegacySpellAnimationTarget(uid, 0, Hit: true, MsgAttackEffect.None)]);
                var animationFrames = animation.ToFrames();

                foreach (var frame in animationFrames)
                {
                    await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                }

                var statusFrame = LegacyMsgUpdatePacket
                    .CreateLegacyRuntimeStatusFlagVectorUpdate(
                        uid,
                        LegacyPlayerDeathState.CreateStatusFlagVector(
                            promotedCharacter.Attributes,
                            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                    .ToFrameBytes();
                await active.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;

                foreach (var viewer in activeSessions)
                {
                    if (ReferenceEquals(viewer.Connection, active.Connection) ||
                        viewer.Session.Character.Uid == promotedCharacter.Uid ||
                        !CanSeePlayer(viewer.Session.Character, promotedCharacter))
                    {
                        continue;
                    }

                    foreach (var frame in animationFrames)
                    {
                        await viewer.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                    }

                    await viewer.Connection.SendAsync(statusFrame, cancellationToken).ConfigureAwait(false);
                    viewerCount++;
                }
            }

            if (!TryExpireLegacyTimedStatusFlag(
                    currentSession.Character,
                    now,
                    LegacyFocusedStatusFlag,
                    WorldPacketHandlers.RuntimeLegacyFocusedExpiresAtUtcAttribute,
                    out var expiredCharacter,
                    out var removedVisibleFlag))
            {
                continue;
            }

            var expiredSession = currentSession with { Character = expiredCharacter };
            _sessions.Set(active.Connection, expiredSession);
            if (!removedVisibleFlag)
            {
                continue;
            }

            var expiredUid = CheckedUInt32(expiredCharacter.Uid, nameof(expiredCharacter.Uid));
            var expiredStatusFrame = LegacyMsgUpdatePacket
                .CreateLegacyRuntimeStatusFlagVectorUpdate(
                    expiredUid,
                    LegacyPlayerDeathState.CreateStatusFlagVector(
                        expiredCharacter.Attributes,
                        LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength))
                .ToFrameBytes();
            await active.Connection.SendAsync(expiredStatusFrame, cancellationToken).ConfigureAwait(false);
            viewerCount++;

            foreach (var viewer in activeSessions)
            {
                if (ReferenceEquals(viewer.Connection, active.Connection) ||
                    viewer.Session.Character.Uid == expiredCharacter.Uid ||
                    !CanSeePlayer(viewer.Session.Character, expiredCharacter))
                {
                    continue;
                }

                await viewer.Connection.SendAsync(expiredStatusFrame, cancellationToken).ConfigureAwait(false);
                viewerCount++;
            }

            expiredCount++;
        }

        if (promotedCount > 0 || expiredCount > 0)
        {
            await _log.WriteLineAsync(
                    "WorldServer processed legacy Intensify focus runtime updates " +
                    $"promotedCount={promotedCount} expiredCount={expiredCount} viewerCount={viewerCount}.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryPromoteLegacyIntensifyFocus(
        CharacterSnapshot character,
        DateTimeOffset now,
        out CharacterSnapshot updatedCharacter,
        out ushort focusSpellId,
        out byte focusSpellLevel,
        out byte useSpellSoul)
    {
        updatedCharacter = character;
        focusSpellId = 0;
        focusSpellLevel = 0;
        useSpellSoul = 0;

        if (!ReadLegacyIntensifyAttributeBool(
                character.Attributes,
                WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute))
        {
            return false;
        }

        if (HasLegacyIntensifyStatusFlag(character, LegacyFocusedStatusFlag) ||
            !character.Attributes.TryGetValue(
                WorldPacketHandlers.RuntimeLegacyIntensifyStampUtcAttribute,
                out var rawStamp) ||
            !DateTimeOffset.TryParse(
                rawStamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var stamp) ||
            stamp.ToUniversalTime().Add(LegacyIntensifyArmDelay) > now.ToUniversalTime())
        {
            return false;
        }

        var spellId = ReadLegacyIntensifyAttributeUInt32(
            character.Attributes,
            WorldPacketHandlers.RuntimeLegacyFocusClientSpellIdAttribute,
            "FocusClientSpellID");
        if (spellId == 0 || spellId > ushort.MaxValue)
        {
            return false;
        }

        var spellLevel = ReadLegacyIntensifyAttributeUInt32(
            character.Attributes,
            WorldPacketHandlers.RuntimeLegacyFocusClientSpellLevelAttribute);
        focusSpellId = (ushort)spellId;
        focusSpellLevel = (byte)Math.Min(spellLevel, byte.MaxValue);
        useSpellSoul = (byte)Math.Min(
            byte.MaxValue,
            ReadLegacyIntensifyAttributeUInt32(
                character.Attributes,
                WorldPacketHandlers.RuntimeLegacyFocusClientSpellUseSpellSoulAttribute));

        updatedCharacter = WithLegacyIntensifyCharacterAttribute(
            WithLegacyIntensifyCharacterAttribute(
                WithLegacyIntensifyCharacterAttribute(
                    character,
                    WorldPacketHandlers.RuntimeLegacyInUseIntensifyAttribute,
                    "false"),
                WorldPacketHandlers.RuntimeLegacyFocusedExpiresAtUtcAttribute,
                now.ToUniversalTime().Add(LegacyFocusedDuration).ToString("O", CultureInfo.InvariantCulture)),
            "LegacyFocusedRemoveOnDead",
            "true");
        updatedCharacter = WithLegacyIntensifyStatusFlag(updatedCharacter, LegacyFocusedStatusFlag);
        return true;
    }

    private static uint ReadLegacyIntensifyAttributeUInt32(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static bool ReadLegacyIntensifyAttributeBool(
        IReadOnlyDictionary<string, string> attributes,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value != 0;
            }
        }

        return false;
    }

    private static CharacterSnapshot WithLegacyIntensifyCharacterAttribute(
        CharacterSnapshot character,
        string key,
        string value)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value,
        };
        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithLegacyIntensifyStatusFlag(CharacterSnapshot character, int flag)
    {
        character = LegacyStatusFlagDeathRegistration.PrepareAdd(character, flag);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        attributes.TryGetValue(key, out var raw);

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = (raw ?? string.Empty).Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordCount = Math.Max(Math.Max(10, parts.Length), wordIndex + 1);
        var words = new ulong[wordCount];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] |= 1UL << bitIndex;
        attributes[key] = string.Join('/', words) + "/";
        return character with { Attributes = attributes };
    }

    private static bool HasLegacyIntensifyStatusFlag(CharacterSnapshot character, int flag)
    {
        var raw = character.Attributes.TryGetValue("StatusFlags", out var statusFlags)
            ? statusFlags
            : character.Attributes.TryGetValue("BitVector", out var bitVector)
                ? bitVector
                : string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wordIndex >= parts.Length ||
            !ulong.TryParse(parts[wordIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word))
        {
            return false;
        }

        return (word & (1UL << bitIndex)) != 0;
    }
}
