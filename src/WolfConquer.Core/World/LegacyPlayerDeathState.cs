using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyPlayerReviveOptions
{
    public TimeSpan ReviveDelay { get; init; } = LegacyPlayerDeathState.DefaultReviveDelay;

    public long? MaximumHitpoints { get; init; }

    public long? MaximumMana { get; init; }

    public ushort Stamina { get; init; } = LegacyPlayerDeathState.DefaultReviveStamina;

    public bool AllowAlive { get; init; }

    public bool IgnoreReviveDelay { get; init; }
}

public enum LegacyPlayerReviveEligibilityReason
{
    Ready,
    Alive,
    WaitingForDelay,
}

public enum LegacyPlayerDeathPromptState
{
    Alive,
    DeadWaitingForReviveDelay,
    DeadReadyForRevive,
}

public enum LegacyPlayerDeathPromptAction
{
    None,
    ShowRevivePrompt,
    ClearRevivePrompt,
}

public readonly record struct LegacyPlayerReviveEligibility(
    bool CanRequestRevive,
    LegacyPlayerReviveEligibilityReason Reason,
    bool IsDead,
    DateTimeOffset? DeadStamp,
    DateTimeOffset? ReviveAllowedAt,
    TimeSpan RemainingDelay)
{
    public bool ShouldPrompt => CanRequestRevive;
}

public readonly record struct LegacyPlayerDeathPromptReadiness(
    LegacyPlayerDeathPromptState State,
    LegacyPlayerDeathPromptAction Action,
    LegacyPlayerReviveEligibility Eligibility)
{
    public bool IsDead => Eligibility.IsDead;

    public bool IsAlive => !IsDead;

    public bool ShouldEmitRevivePrompt => Action == LegacyPlayerDeathPromptAction.ShowRevivePrompt;

    public bool ShouldClearRevivePrompt => Action == LegacyPlayerDeathPromptAction.ClearRevivePrompt;

    public bool ShouldEmitPacket => Action != LegacyPlayerDeathPromptAction.None;

    public DateTimeOffset? DeadStamp => Eligibility.DeadStamp;

    public DateTimeOffset? ReviveAllowedAt => Eligibility.ReviveAllowedAt;

    public TimeSpan RemainingDelay => Eligibility.RemainingDelay;
}

public readonly record struct LegacyPlayerDeathAttributes(
    IReadOnlyDictionary<string, string> Attributes,
    DateTimeOffset DeadStamp,
    DateTimeOffset GhostStamp,
    DateTimeOffset ReviveAllowedAt,
    ushort DeadX,
    ushort DeadY,
    uint? KillerUid,
    string? KillerName)
{
    public bool HasDeadFlag => LegacyPlayerDeathState.HasStatusFlag(Attributes, LegacyPlayerDeathState.DeadStatusFlag);

    public bool HasGhostFlag => LegacyPlayerDeathState.HasStatusFlag(Attributes, LegacyPlayerDeathState.GhostStatusFlag);
}

public readonly record struct LegacyPlayerDeathResult(
    CharacterSnapshot Character,
    LegacyPlayerDeathAttributes State)
{
    public IReadOnlyDictionary<string, string> Attributes => State.Attributes;
}

public readonly record struct LegacyPlayerReviveAttributeResult(
    bool Allowed,
    IReadOnlyDictionary<string, string> Attributes,
    DateTimeOffset? DeadStamp,
    DateTimeOffset? ReviveAllowedAt,
    long Hitpoints,
    long Mana,
    ushort Stamina,
    LegacyPlayerReviveEligibilityReason Reason);

public readonly record struct LegacyPlayerReviveResult(
    bool Allowed,
    CharacterSnapshot Character,
    LegacyPlayerReviveAttributeResult State)
{
    public IReadOnlyDictionary<string, string> Attributes => State.Attributes;
}

public readonly record struct LegacyPlayerGhostResult(
    bool Applied,
    CharacterSnapshot Character,
    IReadOnlyDictionary<string, string> Attributes,
    DateTimeOffset? GhostStamp,
    long TransformationId);

public readonly record struct LegacyPlayerDeadPositionResult(
    bool UsesFallback,
    ushort RecordedX,
    ushort RecordedY,
    ushort ResolvedX,
    ushort ResolvedY,
    ushort InteractionX,
    ushort InteractionY);

public static class LegacyPlayerDeathState
{
    public const int DeadStatusFlag = 0x05;
    public const int GhostStatusFlag = 0x0A;
    public const ushort DefaultReviveStamina = 100;
    public const ushort DefaultDeadPositionFallbackX = 410;
    public const ushort DefaultDeadPositionFallbackY = 354;

    public const string DeadStampAttribute = "DeadStamp";
    public const string GhostStampAttribute = "GhostStamp";
    public const string DeadXAttribute = "Dead_X";
    public const string DeadYAttribute = "Dead_Y";
    public const string DeadKillerUidAttribute = "DeadKillerUID";
    public const string DeadKillerNameAttribute = "DeadKillerName";
    public const string RevivePromptVisibleAttribute = "RevivePromptVisible";
    public const string RevivePromptPendingKey = "DeathRevivePrompt";
    public const string RevivePromptText = "You can revive now.";
    public const string DeadTalkText = "You are dead.";

    private const string LegacyRideExpiresAtUtcAttribute = "LegacyRideExpiresAtUtc";
    private const string LegacyRideRemoveOnDeadAttribute = "LegacyRideRemoveOnDead";
    private const string LegacyRideStampSecondsAttribute = "LegacyRideStampSeconds";

    public static readonly TimeSpan DefaultReviveDelay = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan GhostEffectDelay = TimeSpan.FromMilliseconds(1000);

    public static LegacyPlayerDeathResult ApplyDeath(
        CharacterSnapshot character,
        DateTimeOffset now,
        ushort deadX,
        ushort deadY,
        uint? killerUid = null,
        string? killerName = null,
        TimeSpan? reviveDelay = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        var state = ApplyDeath(
            character.Attributes,
            now,
            deadX,
            deadY,
            killerUid,
            killerName,
            reviveDelay);

        return new LegacyPlayerDeathResult(
            character with
            {
                MinHitPoints = 0,
                Attributes = state.Attributes,
            },
            state);
    }

    public static LegacyPlayerDeathAttributes ApplyDeath(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset now,
        ushort deadX,
        ushort deadY,
        uint? killerUid = null,
        string? killerName = null,
        TimeSpan? reviveDelay = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var delay = reviveDelay ?? DefaultReviveDelay;
        var deadStamp = now;
        var ghostStamp = now + GhostEffectDelay;
        var updated = CopyAttributes(attributes);
        SetOnlyStatusFlags(updated, DeadStatusFlag);
        WorldPacketHandlers.RemoveLegacyAuraDeathMetadata(updated);
        updated.Remove(LegacyRideExpiresAtUtcAttribute);
        updated.Remove(LegacyRideRemoveOnDeadAttribute);
        updated.Remove(LegacyRideStampSecondsAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyStigmaRemoveOnDeadAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyAccuracyExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyAccuracyRemoveOnDeadAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyInvisibilityExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyInvisibilityRemoveOnDeadAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacySoulShackleExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacySoulShackleRemoveOnDeadAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyFrostArrowsExpiresAtUtcAttribute);
        updated.Remove(WorldPacketHandlers.RuntimeLegacyFrostArrowsRemoveOnDeadAttribute);
        updated.Remove(LegacyControlStatusLifetime.FrightenedExpiresAtUtcAttribute);
        updated.Remove(LegacyControlStatusLifetime.FrightenedRemoveOnDeadAttribute);
        updated.Remove(LegacyControlStatusLifetime.DizzyExpiresAtUtcAttribute);
        updated.Remove(LegacyControlStatusLifetime.DizzyRemoveOnDeadAttribute);
        updated.Remove("LegacyHawksAmbitionExpiresAtUtc");
        updated.Remove("LegacyHawksAmbitionRemoveOnDead");
        updated.Remove("LegacyBonePulseExpiresAtUtc");
        updated.Remove("BonePulseExpiresAtUtc");
        updated.Remove("LegacyBonePulseRemoveOnDead");
        updated.Remove("LegacyMudWallExpiresAtUtc");
        updated.Remove("LegacyMudWallRemoveOnDead");
        SetNumericAttribute(updated, "MinHitPoints", "HitPoints", 0);
        updated[DeadStampAttribute] = FormatTimestamp(deadStamp);
        updated[GhostStampAttribute] = FormatTimestamp(ghostStamp);
        updated[DeadXAttribute] = deadX.ToString(CultureInfo.InvariantCulture);
        updated[DeadYAttribute] = deadY.ToString(CultureInfo.InvariantCulture);
        updated.Remove(RevivePromptVisibleAttribute);

        if (killerUid is { } uid)
        {
            updated[DeadKillerUidAttribute] = uid.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            updated.Remove(DeadKillerUidAttribute);
        }

        if (!string.IsNullOrWhiteSpace(killerName))
        {
            updated[DeadKillerNameAttribute] = killerName;
        }
        else
        {
            updated.Remove(DeadKillerNameAttribute);
        }

        return new LegacyPlayerDeathAttributes(
            updated,
            deadStamp,
            ghostStamp,
            deadStamp + delay,
            deadX,
            deadY,
            killerUid,
            string.IsNullOrWhiteSpace(killerName) ? null : killerName);
    }

    public static LegacyPlayerReviveResult TryRevive(
        CharacterSnapshot character,
        DateTimeOffset now,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        var effectiveOptions = options ?? new LegacyPlayerReviveOptions();
        var maximumHitpoints = ResolveMaximumHitpoints(
            character.Attributes,
            effectiveOptions.MaximumHitpoints,
            Math.Max(character.MinHitPoints, 1));
        var maximumMana = ResolveMaximumMana(
            character.Attributes,
            effectiveOptions.MaximumMana,
            character.MaxMana);
        var state = TryRevive(
            character.Attributes,
            now,
            currentHitpoints: character.MinHitPoints,
            maximumHitpoints,
            currentMana: character.MinMana,
            maximumMana,
            effectiveOptions);
        if (!state.Allowed)
        {
            return new LegacyPlayerReviveResult(false, character, state);
        }

        var revived = character with
        {
            MinHitPoints = state.Hitpoints,
            MinMana = state.Mana,
            Attributes = state.Attributes,
        };
        return new LegacyPlayerReviveResult(true, revived, state);
    }

    public static LegacyPlayerReviveAttributeResult TryRevive(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset now,
        long currentHitpoints,
        long maximumHitpoints,
        long currentMana,
        long maximumMana,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var effectiveOptions = options ?? new LegacyPlayerReviveOptions();
        var eligibility = EvaluateReviveEligibility(attributes, now, currentHitpoints, effectiveOptions);
        if (!eligibility.CanRequestRevive)
        {
            return new LegacyPlayerReviveAttributeResult(
                false,
                attributes,
                eligibility.DeadStamp,
                eligibility.ReviveAllowedAt,
                currentHitpoints,
                currentMana,
                effectiveOptions.Stamina,
                eligibility.Reason);
        }

        var updated = CopyAttributes(attributes);
        ClearStatusFlags(updated);
        updated.Remove(RevivePromptVisibleAttribute);
        if (updated.TryGetValue(LegacyRuntimeMessageBoxPrompt.PendingAttribute, out var pending) &&
            string.Equals(pending, RevivePromptPendingKey, StringComparison.Ordinal))
        {
            updated.Remove(LegacyRuntimeMessageBoxPrompt.PendingAttribute);
            updated.Remove(LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute);
            updated.Remove(LegacyRuntimeMessageBoxPrompt.DestinationXAttribute);
            updated.Remove(LegacyRuntimeMessageBoxPrompt.DestinationYAttribute);
            updated.Remove(LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute);
            updated.Remove(LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute);
        }

        var hitpoints = ClampToRange(maximumHitpoints, 1, long.MaxValue);
        var mana = ClampToRange(currentMana, 0, Math.Max(0, maximumMana));

        SetNumericAttribute(updated, "MinHitPoints", "HitPoints", hitpoints);
        SetNumericAttribute(updated, "MinMana", "Mana", mana);
        SetNumericAttribute(updated, "TransformationID", "TransformationId", 0);
        updated["Stamina"] = effectiveOptions.Stamina.ToString(CultureInfo.InvariantCulture);

        return new LegacyPlayerReviveAttributeResult(
            true,
            updated,
            eligibility.DeadStamp,
            eligibility.ReviveAllowedAt,
            hitpoints,
            mana,
            effectiveOptions.Stamina,
            eligibility.Reason);
    }

    public static LegacyPlayerReviveEligibility EvaluateReviveEligibility(
        CharacterSnapshot character,
        DateTimeOffset now,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        return EvaluateReviveEligibility(character.Attributes, now, character.MinHitPoints, options);
    }

    public static LegacyPlayerReviveEligibility EvaluateReviveEligibility(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset now,
        long currentHitpoints,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var effectiveOptions = options ?? new LegacyPlayerReviveOptions();
        var isDead = currentHitpoints <= 0 || HasStatusFlag(attributes, DeadStatusFlag);
        var recordedDeadStamp = TryReadTimestamp(attributes, DeadStampAttribute, out var parsedDeadStamp)
            ? parsedDeadStamp
            : (DateTimeOffset?)null;
        var reviveAllowedAt = recordedDeadStamp + effectiveOptions.ReviveDelay;

        if (!isDead && !effectiveOptions.AllowAlive)
        {
            return new LegacyPlayerReviveEligibility(
                false,
                LegacyPlayerReviveEligibilityReason.Alive,
                false,
                recordedDeadStamp,
                reviveAllowedAt,
                TimeSpan.Zero);
        }

        if (!effectiveOptions.IgnoreReviveDelay &&
            reviveAllowedAt is { } allowedAt &&
            now <= allowedAt)
        {
            return new LegacyPlayerReviveEligibility(
                false,
                LegacyPlayerReviveEligibilityReason.WaitingForDelay,
                isDead,
                recordedDeadStamp,
                reviveAllowedAt,
                Max(TimeSpan.Zero, allowedAt - now));
        }

        return new LegacyPlayerReviveEligibility(
            true,
            LegacyPlayerReviveEligibilityReason.Ready,
            isDead,
            recordedDeadStamp,
            reviveAllowedAt,
            TimeSpan.Zero);
    }

    public static LegacyPlayerDeathPromptReadiness EvaluateDeathPromptReadiness(
        CharacterSnapshot character,
        DateTimeOffset now,
        bool revivePromptVisible = false,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        return EvaluateDeathPromptReadiness(
            character.Attributes,
            now,
            character.MinHitPoints,
            revivePromptVisible,
            options);
    }

    public static LegacyPlayerDeathPromptReadiness EvaluateDeathPromptReadiness(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset now,
        long currentHitpoints,
        bool revivePromptVisible = false,
        LegacyPlayerReviveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var eligibility = EvaluateReviveEligibility(attributes, now, currentHitpoints, options);
        if (!eligibility.IsDead)
        {
            return new LegacyPlayerDeathPromptReadiness(
                LegacyPlayerDeathPromptState.Alive,
                revivePromptVisible
                    ? LegacyPlayerDeathPromptAction.ClearRevivePrompt
                    : LegacyPlayerDeathPromptAction.None,
                eligibility);
        }

        if (eligibility.CanRequestRevive)
        {
            return new LegacyPlayerDeathPromptReadiness(
                LegacyPlayerDeathPromptState.DeadReadyForRevive,
                revivePromptVisible
                    ? LegacyPlayerDeathPromptAction.None
                    : LegacyPlayerDeathPromptAction.ShowRevivePrompt,
                eligibility);
        }

        return new LegacyPlayerDeathPromptReadiness(
            LegacyPlayerDeathPromptState.DeadWaitingForReviveDelay,
            LegacyPlayerDeathPromptAction.None,
            eligibility);
    }

    public static bool CanRevive(
        IReadOnlyDictionary<string, string> attributes,
        DateTimeOffset now,
        TimeSpan? reviveDelay = null)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        if (!TryReadTimestamp(attributes, DeadStampAttribute, out var deadStamp))
        {
            return true;
        }

        return now > deadStamp + (reviveDelay ?? DefaultReviveDelay);
    }

    public static LegacyPlayerGhostResult TryApplyGhost(CharacterSnapshot character, DateTimeOffset now)
    {
        return TryApplyGhost(character, now, includeTransformation: true);
    }

    internal static LegacyPlayerGhostResult TryEnterGhost(CharacterSnapshot character, DateTimeOffset now)
    {
        return TryApplyGhost(character, now, includeTransformation: false);
    }

    private static LegacyPlayerGhostResult TryApplyGhost(
        CharacterSnapshot character, DateTimeOffset now, bool includeTransformation)
    {
        ArgumentNullException.ThrowIfNull(character);

        var ghostStamp = TryReadTimestamp(character.Attributes, GhostStampAttribute, out var parsedGhostStamp)
            ? parsedGhostStamp
            : (DateTimeOffset?)null;
        var isDead = character.MinHitPoints <= 0 || HasStatusFlag(character.Attributes, DeadStatusFlag);
        if (!isDead ||
            HasStatusFlag(character.Attributes, GhostStatusFlag) ||
            ghostStamp is null ||
            now <= ghostStamp.Value)
        {
            return new LegacyPlayerGhostResult(
                false,
                character,
                character.Attributes,
                ghostStamp,
                ResolveTransformationId(character.Body));
        }

        var updated = CopyAttributes(character.Attributes);
        AddStatusFlag(updated, GhostStatusFlag);
        var transformationId = ResolveTransformationId(character.Body);
        if (includeTransformation)
            SetNumericAttribute(updated, "TransformationID", "TransformationId", transformationId);

        var ghosted = character with { Attributes = updated };
        return new LegacyPlayerGhostResult(true, ghosted, updated, ghostStamp, transformationId);
    }

    internal static CharacterSnapshot ApplyEnteredGhostTransformation(
        CharacterSnapshot character, out long transformationId)
    {
        ArgumentNullException.ThrowIfNull(character);
        transformationId = ResolveTransformationId(character.Body);
        var updated = CopyAttributes(character.Attributes);
        SetNumericAttribute(updated, "TransformationID", "TransformationId", transformationId);
        return character with { Attributes = updated };
    }

    public static LegacyPlayerDeadPositionResult ResolveDeadPosition(
        CharacterSnapshot character,
        ushort fallbackX = DefaultDeadPositionFallbackX,
        ushort fallbackY = DefaultDeadPositionFallbackY)
    {
        ArgumentNullException.ThrowIfNull(character);

        return ResolveDeadPosition(character.Attributes, fallbackX, fallbackY);
    }

    public static LegacyPlayerDeadPositionResult ResolveDeadPosition(
        IReadOnlyDictionary<string, string> attributes,
        ushort fallbackX = DefaultDeadPositionFallbackX,
        ushort fallbackY = DefaultDeadPositionFallbackY)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var recordedX = TryReadUInt16(attributes, DeadXAttribute, out var parsedX) ? parsedX : (ushort)0;
        var recordedY = TryReadUInt16(attributes, DeadYAttribute, out var parsedY) ? parsedY : (ushort)0;
        var usesFallback = recordedX == 0 || recordedY == 0;

        return new LegacyPlayerDeadPositionResult(
            usesFallback,
            recordedX,
            recordedY,
            usesFallback ? fallbackX : recordedX,
            usesFallback ? fallbackY : recordedY,
            recordedX,
            recordedY);
    }

    public static ulong[] CreateStatusFlagVector(
        IReadOnlyDictionary<string, string> attributes,
        int length)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "Vector length cannot be negative.");
        }

        var values = new ulong[length];
        var raw = ReadStatusFlags(attributes);
        if (raw.Length == 0)
        {
            return values;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < Math.Min(parts.Length, values.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index]);
        }

        return values;
    }

    public static bool HasStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        if (flag < 0)
        {
            return false;
        }

        var raw = ReadStatusFlags(attributes);
        if (raw.Length == 0)
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

        var mask = 1UL << bitIndex;
        return (word & mask) == mask;
    }

    private static Dictionary<string, string> CopyAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        return new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
    }

    private static void SetOnlyStatusFlags(Dictionary<string, string> attributes, params int[] flags)
    {
        if (!flags.Contains(LegacyLightningShieldActivationLifetime.StatusFlag))
            LegacyLightningShieldActivationLifetime.Forget(attributes);
        if (!flags.Contains(LegacyLightningShieldBaseLifetime.StatusFlag))
            LegacyLightningShieldBaseLifetime.Forget(attributes);
        LegacyStatusFlagDeathRegistration.ForgetAll(attributes);
        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        attributes.Remove("StatusFlags");
        attributes.Remove("BitVector");

        var maxFlag = flags.Where(flag => flag >= 0).DefaultIfEmpty(0).Max();
        var words = new ulong[Math.Max(10, maxFlag / 32 + 1)];
        foreach (var flag in flags)
        {
            if (flag < 0)
            {
                continue;
            }

            words[flag / 32] |= 1UL << (flag % 32);
        }

        attributes[key] = string.Join('/', words) + "/";
    }

    private static void AddStatusFlag(Dictionary<string, string> attributes, int flag)
    {
        LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, flag);
        if (flag < 0)
        {
            return;
        }

        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        var raw = attributes.TryGetValue(key, out var existingRaw) ? existingRaw ?? "" : "";
        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var words = new ulong[Math.Max(10, Math.Max(parts.Length, wordIndex + 1))];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] |= 1UL << bitIndex;
        attributes[key] = string.Join('/', words) + "/";
    }

    private static void ClearStatusFlags(Dictionary<string, string> attributes)
    {
        LegacyLightningShieldBaseLifetime.Forget(attributes);
        LegacyStatusFlagDeathRegistration.ForgetAll(attributes);
        attributes.Remove("StatusFlags");
        attributes.Remove("BitVector");
    }

    private static string ReadStatusFlags(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("StatusFlags", out var statusFlags))
        {
            return statusFlags ?? "";
        }

        return attributes.TryGetValue("BitVector", out var bitVector) ? bitVector ?? "" : "";
    }

    private static long ResolveTransformationId(long body)
    {
        return body % 10 == 7 ? 99 : 98;
    }

    private static void SetNumericAttribute(
        Dictionary<string, string> attributes,
        string preferredKey,
        string alternateKey,
        long value)
    {
        var key = attributes.ContainsKey(preferredKey)
            ? preferredKey
            : attributes.ContainsKey(alternateKey)
                ? alternateKey
                : preferredKey;
        attributes[key] = value.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryReadTimestamp(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!attributes.TryGetValue(key, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp))
        {
            return true;
        }

        if (!DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var legacyDateTime))
        {
            return false;
        }

        timestamp = new DateTimeOffset(legacyDateTime);
        return true;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToString("O", CultureInfo.InvariantCulture);
    }

    private static long ResolveMaximumHitpoints(
        IReadOnlyDictionary<string, string> attributes,
        long? configuredMaximum,
        long fallback)
    {
        if (configuredMaximum is { } maximum)
        {
            return ClampToRange(maximum, 1, long.MaxValue);
        }

        if (TryReadInt64(attributes, out var attributeMaximum, "MaxHitpoints", "MaxHitPoints", "MaxLife"))
        {
            return ClampToRange(attributeMaximum, 1, long.MaxValue);
        }

        return ClampToRange(fallback, 1, long.MaxValue);
    }

    private static long ResolveMaximumMana(
        IReadOnlyDictionary<string, string> attributes,
        long? configuredMaximum,
        long fallback)
    {
        if (configuredMaximum is { } maximum)
        {
            return Math.Max(0, maximum);
        }

        if (TryReadInt64(attributes, out var attributeMaximum, "MaxMana"))
        {
            return Math.Max(0, attributeMaximum);
        }

        return Math.Max(0, fallback);
    }

    private static bool TryReadInt64(
        IReadOnlyDictionary<string, string> attributes,
        out long value,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var raw) ||
                !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                continue;
            }

            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadUInt16(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out ushort value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) &&
            ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static long ClampToRange(long value, long minimum, long maximum)
    {
        return Math.Min(maximum, Math.Max(minimum, value));
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right)
    {
        return left >= right ? left : right;
    }
}
