using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only real StoneCracker producers install these source-local Flag.Timer registrations.
internal sealed record LegacyArrowBladesFlagRegistration(
    long OwnerUid, int Flag, Guid Identity, DateTime Timer, int Seconds)
{
    public const int FireFlag = 332;
    public const int WaterFlag = 333;
    public const int StarFlowFlag = 337;
    public const int StarFlowTriggerFlag = 336;
    public const int FireSeconds = 10;
    public const int WaterSeconds = 2_592_000;
    private static readonly string[] Keys = ["OwnerUid", "Identity", "Seconds", "TimerTicks"];
    public DateTime ExpiresAtLocal => Timer.AddSeconds(Seconds);

    public static bool IsFamilyFlag(int flag) => flag is FireFlag or WaterFlag or StarFlowFlag or StarFlowTriggerFlag;

    public static DateTime LocalTime(TimeProvider provider, DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, provider.LocalTimeZone).DateTime;

    public static LegacyArrowBladesFlagRegistration Create(long ownerUid, int flag, TimeProvider provider)
    {
        if (!IsFamilyFlag(flag)) throw new ArgumentOutOfRangeException(nameof(flag));
        return new(ownerUid, flag, Guid.NewGuid(), LocalTime(provider, provider.GetUtcNow()),
            flag is FireFlag or StarFlowTriggerFlag ? FireSeconds : WaterSeconds);
    }

    private static string Prefix(int flag) => $"LegacyArrowBladesFlag.{flag}.";

    public static bool TryRead(
        CharacterSnapshot character, int flag, out LegacyArrowBladesFlagRegistration registration)
    {
        registration = null!;
        if (!IsFamilyFlag(flag) || !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag))
            return false;
        var attributes = character.Attributes;
        var prefix = Prefix(flag);
        if (!attributes.TryGetValue(prefix + "OwnerUid", out var ownerRaw) ||
            !long.TryParse(ownerRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var owner) ||
            owner != character.Uid ||
            !attributes.TryGetValue(prefix + "Identity", out var identityRaw) ||
            !Guid.TryParseExact(identityRaw, "D", out var identity) || identity == Guid.Empty ||
            !attributes.TryGetValue(prefix + "Seconds", out var secondsRaw) ||
            !int.TryParse(secondsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            seconds != (flag is FireFlag or StarFlowTriggerFlag ? FireSeconds : WaterSeconds) ||
            !attributes.TryGetValue(prefix + "TimerTicks", out var timerRaw) ||
            !long.TryParse(timerRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks) ||
            ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks - TimeSpan.TicksPerSecond * seconds)
            return false;
        registration = new(owner, flag, identity, new DateTime(ticks, DateTimeKind.Unspecified), seconds);
        return true;
    }

    public CharacterSnapshot Install(CharacterSnapshot character)
    {
        if (character.Uid != OwnerUid || !IsFamilyFlag(Flag) ||
            !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, Flag))
            throw new ArgumentException("ArrowBlades registration requires its active original owner flag.", nameof(character));
        var prefix = Prefix(Flag);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [prefix + "OwnerUid"] = OwnerUid.ToString(CultureInfo.InvariantCulture),
            [prefix + "Identity"] = Identity.ToString("D"),
            [prefix + "Seconds"] = Seconds.ToString(CultureInfo.InvariantCulture),
            [prefix + "TimerTicks"] = Timer.Ticks.ToString(CultureInfo.InvariantCulture),
        };
        return character with { Attributes = attributes };
    }

    public static CharacterSnapshot Forget(CharacterSnapshot character, int flag)
    {
        if (!IsFamilyFlag(flag) || !Keys.Any(key => character.Attributes.ContainsKey(Prefix(flag) + key)))
            return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        Forget(attributes, flag);
        return character with { Attributes = attributes };
    }

    public static void Forget(Dictionary<string, string> attributes, int flag)
    {
        if (!IsFamilyFlag(flag)) return;
        var prefix = Prefix(flag);
        foreach (var key in Keys) attributes.Remove(prefix + key);
    }
}
