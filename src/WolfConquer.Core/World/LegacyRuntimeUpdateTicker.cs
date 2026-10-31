using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed class LegacyRuntimeUpdateTicker
{
    public const int XpListFlag = 0x04;
    public const int OverwhelmFlag = 0xF3;
    public const int RevelatorFlag = 0x198;

    private static readonly TimeSpan XpRecurringDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan StandingStaminaRecurringDelay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SittingStaminaRecurringDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OnlinePointsDelay = TimeSpan.FromMinutes(10);
    private const ulong XpListActivationCount = 100;
    private const ushort StandingStaminaIncrement = 5;
    private const ushort SittingStaminaIncrement = 15;
    private const ulong LegacyConquerActionSit = 250;
    private const string OnlinePointsMessage = "You get 1 Online Points.";

    private readonly uint _uid;
    private readonly ulong[] _statusFlags;
    private readonly bool _hasRuntimeStatusFlags;
    private readonly bool _hasOverwhelmRune;
    private readonly bool _hasRevelatorStartupEffect;
    private readonly LegacyTopInHoursInvitationScheduler _topInHoursInvitations = new();
    private DateTimeOffset _nextXpAt;
    private DateTimeOffset _lastStaminaAt;
    private DateTimeOffset _nextOnlinePointsAt;
    private DateTimeOffset _xpListExpiresAt;
    private ulong _xpCount;
    private ushort _maximumStamina;
    private ushort _stamina;
    private bool _sentStatusFlags;
    private bool _xpListActive;
    private bool _isSitting;

    public LegacyRuntimeUpdateTicker(
        CharacterSnapshot snapshot,
        DateTimeOffset startTime,
        bool hasOverwhelmRune,
        bool hasRevelatorStartupEffect)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        _maximumStamina = ReadLegacyMaximumStamina(snapshot);
        _stamina = TryReadLegacyCurrentStamina(snapshot, out var stamina) ? stamina : (ushort)0;
        _isSitting = IsLegacyConquerActionSit(snapshot);
        _hasOverwhelmRune = hasOverwhelmRune;
        _hasRevelatorStartupEffect = hasRevelatorStartupEffect;
        _statusFlags = CreateRuntimeStatusFlags(snapshot, hasOverwhelmRune, hasRevelatorStartupEffect);
        _hasRuntimeStatusFlags = HasAnyFlag(_statusFlags);
        _xpCount = ReadLegacyXpCount(snapshot);
        _xpListActive = LegacyPlayerDeathState.HasStatusFlag(snapshot.Attributes, XpListFlag);
        if (_xpListActive)
        {
            SetFlag(_statusFlags, XpListFlag);
            _xpListExpiresAt = startTime.AddSeconds(20);
        }

        _nextXpAt = startTime;
        _lastStaminaAt = startTime - StandingStaminaRecurringDelay;
        _nextOnlinePointsAt = startTime + OnlinePointsDelay;
    }

    public IReadOnlyList<byte[]> CreateDueFrameBytes(DateTimeOffset now)
    {
        var updates = CreateDueUpdates(now);
        if (updates.Count == 0)
        {
            return [];
        }

        var frames = new byte[updates.Count][];
        for (var index = 0; index < updates.Count; index++)
        {
            frames[index] = updates[index].FrameBytes;
        }

        return frames;
    }

    public IReadOnlyList<LegacyRuntimeUpdate> CreateDueUpdates(
        DateTimeOffset now,
        CharacterSnapshot? currentSnapshot = null) => CreateDueUpdatesCore(now, currentSnapshot, includeStamina: true);

    // Scheduling/codec compatibility only. The actual admitted Frame routes
    // stamina through its separately owned commit-before-send phase.
    internal IReadOnlyList<LegacyRuntimeUpdate> CreateDueNonStaminaUpdates(DateTimeOffset now,
        CharacterSnapshot? currentSnapshot) => CreateDueUpdatesCore(now, currentSnapshot, includeStamina: false);

    private IReadOnlyList<LegacyRuntimeUpdate> CreateDueUpdatesCore(DateTimeOffset now,
        CharacterSnapshot? currentSnapshot, bool includeStamina)
    {
        var updates = new List<LegacyRuntimeUpdate>(capacity: 5);
        updates.AddRange(_topInHoursInvitations.CreateDueUpdates(now));
        SyncStaminaRuntimeState(currentSnapshot);
        SyncXpRuntimeState(currentSnapshot);

        if (_xpListActive && now >= _xpListExpiresAt)
        {
            _xpListActive = false;
            ClearFlag(_statusFlags, XpListFlag);
            updates.Add(new LegacyRuntimeUpdate(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(_uid, value: 0).ToFrameBytes()));
            updates.Add(new LegacyRuntimeUpdate(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(_uid, _statusFlags).ToFrameBytes(),
                RuntimeXpCount: _xpCount,
                RuntimeXpListActive: false));
        }

        if (now >= _nextXpAt)
        {
            _nextXpAt = now + XpRecurringDelay;
            if (!_xpListActive)
            {
                _xpCount++;
                updates.Add(new LegacyRuntimeUpdate(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(_uid, _xpCount).ToFrameBytes(),
                    RuntimeXpCount: _xpCount));

                if (_xpCount >= XpListActivationCount)
                {
                    _xpCount = 0;
                    _xpListActive = true;
                    _xpListExpiresAt = now.AddSeconds(20);
                    SetFlag(_statusFlags, XpListFlag);
                    updates.Add(new LegacyRuntimeUpdate(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(_uid, _statusFlags).ToFrameBytes(),
                        RuntimeXpCount: _xpCount,
                        RuntimeXpListActive: true));
                    updates.Add(new LegacyRuntimeUpdate(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(_uid).ToFrameBytes()));
                    updates.Add(new LegacyRuntimeUpdate(LegacyStringPacket.PlayerEffect(_uid, "xp").ToFrameBytes()));
                    _sentStatusFlags = true;
                }
            }
        }

        var staminaRecurringDelay = _isSitting ? SittingStaminaRecurringDelay : StandingStaminaRecurringDelay;
        if (includeStamina && now >= _lastStaminaAt + staminaRecurringDelay && _stamina < _maximumStamina)
        {
            var staminaIncrement = _isSitting ? SittingStaminaIncrement : StandingStaminaIncrement;
            _stamina = checked((ushort)Math.Min(_maximumStamina, _stamina + staminaIncrement));
            updates.Add(new LegacyRuntimeUpdate(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(_uid, _stamina).ToFrameBytes(),
                RuntimeStamina: _stamina));
            _lastStaminaAt = now;

            if (_hasRuntimeStatusFlags && !_sentStatusFlags)
            {
                updates.Add(new LegacyRuntimeUpdate(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(_uid, _statusFlags).ToFrameBytes()));
                _sentStatusFlags = true;
            }
        }

        if (now >= _nextOnlinePointsAt)
        {
            updates.Add(new LegacyRuntimeUpdate(LegacyMsgMessagePacket.SystemTalk(OnlinePointsMessage).ToFrameBytes()));
            _nextOnlinePointsAt = now + OnlinePointsDelay;
        }

        return updates;
    }

    private static ulong[] CreateRuntimeStatusFlags(
        CharacterSnapshot snapshot,
        bool hasOverwhelmRune,
        bool hasRevelatorStartupEffect)
    {
        var values = LegacyPlayerDeathState.CreateStatusFlagVector(
            snapshot.Attributes,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        if (hasOverwhelmRune)
        {
            SetFlag(values, OverwhelmFlag);
        }

        if (hasRevelatorStartupEffect)
        {
            SetFlag(values, RevelatorFlag);
        }

        return values;
    }

    private static void SetFlag(ulong[] values, int flag)
    {
        var word = flag / 32;
        var bit = flag % 32;
        if ((uint)word >= values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(flag), flag, "Legacy status flag is outside the runtime vector.");
        }

        values[word] |= 1UL << bit;
    }

    private static void ClearFlag(ulong[] values, int flag)
    {
        var word = flag / 32;
        var bit = flag % 32;
        if ((uint)word >= values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(flag), flag, "Legacy status flag is outside the runtime vector.");
        }

        values[word] &= ~(1UL << bit);
    }

    private static bool HasAnyFlag(IReadOnlyList<ulong> values)
    {
        foreach (var value in values)
        {
            if (value != 0)
            {
                return true;
            }
        }

        return false;
    }

    private void SyncStaminaRuntimeState(CharacterSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        _maximumStamina = ReadLegacyMaximumStamina(snapshot);
        if (TryReadLegacyCurrentStamina(snapshot, out var stamina))
        {
            _stamina = stamina;
        }

        _isSitting = IsLegacyConquerActionSit(snapshot);
    }

    private void SyncXpRuntimeState(CharacterSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        var sessionFlags = LegacyPlayerDeathState.CreateStatusFlagVector(
            snapshot.Attributes,
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength);
        sessionFlags.CopyTo(_statusFlags, 0);
        if (_hasOverwhelmRune)
        {
            SetFlag(_statusFlags, OverwhelmFlag);
        }

        if (_hasRevelatorStartupEffect)
        {
            SetFlag(_statusFlags, RevelatorFlag);
        }

        var sessionXpListActive = LegacyPlayerDeathState.HasStatusFlag(snapshot.Attributes, XpListFlag);
        if (_xpListActive && !sessionXpListActive)
        {
            _xpListActive = false;
            _xpCount = ReadLegacyXpCount(snapshot);
            ClearFlag(_statusFlags, XpListFlag);
        }
        else if (!_xpListActive && sessionXpListActive)
        {
            _xpListActive = true;
            _xpListExpiresAt = DateTimeOffset.MaxValue;
            SetFlag(_statusFlags, XpListFlag);
        }
        else if (!_xpListActive)
        {
            _xpCount = ReadLegacyXpCount(snapshot);
        }
    }

    private static ulong ReadLegacyXpCount(CharacterSnapshot snapshot)
    {
        return snapshot.Attributes.TryGetValue("XPCount", out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var xpCount)
                ? Math.Min(xpCount, XpListActivationCount - 1)
                : 0;
    }

    private static bool TryReadLegacyCurrentStamina(CharacterSnapshot snapshot, out ushort parsed)
    {
        if (snapshot.Attributes.TryGetValue("Stamina", out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stamina))
        {
            parsed = stamina > ushort.MaxValue ? ushort.MaxValue : (ushort)stamina;
            return true;
        }

        parsed = 0;
        return false;
    }

    private static bool IsLegacyConquerActionSit(CharacterSnapshot snapshot)
    {
        if (!snapshot.Attributes.TryGetValue("Action", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw.Equals("Sit", StringComparison.OrdinalIgnoreCase) ||
            (ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var action) &&
                action == LegacyConquerActionSit);
    }

    private static ushort ReadLegacyMaximumStamina(CharacterSnapshot snapshot)
    {
        if (snapshot.Attributes.TryGetValue("HeavenBlessing", out var raw) &&
            ulong.TryParse(raw, out var heavenBlessing) &&
            heavenBlessing > 0)
        {
            return 150;
        }

        if (snapshot.Attributes.TryGetValue("HeavensBlessing", out raw) &&
            ulong.TryParse(raw, out heavenBlessing) &&
            heavenBlessing > 0)
        {
            return 150;
        }

        return 100;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy runtime update value must fit UInt32.");
        }

        return (uint)value;
    }
}
