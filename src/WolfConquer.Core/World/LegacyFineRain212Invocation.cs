using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// One real14500 AddSpellFlag registration. Captures remain valid after replacement/removal.
internal sealed record LegacyFineRain212Invocation(
    long OwnerUid, Guid Identity, int Seconds, DateTime Timer, DateTime TimerInvoker)
{
    private const string Prefix = "LegacyFineRain212Invocation.";
    private static readonly string[] Keys = ["OwnerUid", "Identity", "Seconds", "TimerTicks", "InvokerTicks"];

    public static DateTime LocalTime(TimeProvider provider, DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, provider.LocalTimeZone).DateTime;

    public static LegacyFineRain212Invocation Create(
        long ownerUid, uint duration, TimeProvider provider, out DateTimeOffset expiresAtUtc)
    {
        var timer = provider.GetUtcNow();
        var seconds = unchecked((int)duration);
        expiresAtUtc = timer.AddSeconds(seconds);
        // Flag ctor reads DateTime.Now separately for Timer and TimerInvoker.
        return new(ownerUid, Guid.NewGuid(), seconds, LocalTime(provider, timer),
            LocalTime(provider, provider.GetUtcNow()));
    }

    public static bool TryRead(CharacterSnapshot character, out LegacyFineRain212Invocation invocation)
    {
        invocation = null!;
        var a = character.Attributes;
        if (!LegacyPlayerDeathState.HasStatusFlag(a, 212) ||
            !a.TryGetValue(Prefix + "OwnerUid", out var ownerRaw) ||
            !long.TryParse(ownerRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var owner) ||
            owner != character.Uid ||
            !a.TryGetValue(Prefix + "Identity", out var idRaw) || !Guid.TryParse(idRaw, out var id) ||
            !a.TryGetValue(Prefix + "Seconds", out var secondsRaw) ||
            !int.TryParse(secondsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            !TryReadDate(a, "TimerTicks", out var timer) || !TryReadDate(a, "InvokerTicks", out var invoker))
            return false;
        invocation = new(owner, id, seconds, timer, invoker);
        return true;
    }

    private static bool TryReadDate(IReadOnlyDictionary<string, string> attributes, string key, out DateTime value)
    {
        value = default;
        if (!attributes.TryGetValue(Prefix + key, out var raw) ||
            !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return false;
        value = new DateTime(ticks, DateTimeKind.Unspecified);
        return true;
    }

    public CharacterSnapshot Install(CharacterSnapshot character)
    {
        if (character.Uid != OwnerUid) throw new ArgumentException("FineRain registration owner changed.", nameof(character));
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [Prefix + "OwnerUid"] = OwnerUid.ToString(CultureInfo.InvariantCulture),
            [Prefix + "Identity"] = Identity.ToString("D"),
            [Prefix + "Seconds"] = Seconds.ToString(CultureInfo.InvariantCulture),
            [Prefix + "TimerTicks"] = Timer.Ticks.ToString(CultureInfo.InvariantCulture),
            [Prefix + "InvokerTicks"] = TimerInvoker.Ticks.ToString(CultureInfo.InvariantCulture),
        };
        return character with { Attributes = attributes };
    }

    public static CharacterSnapshot Forget(CharacterSnapshot character)
    {
        if (!Keys.Any(key => character.Attributes.ContainsKey(Prefix + key))) return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var key in Keys) attributes.Remove(Prefix + key);
        return character with { Attributes = attributes };
    }
}
