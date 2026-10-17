using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Only the scoped10950 CanAttackPlayer producer owns this transient registration.
internal sealed record LegacyCanAttackFlashingRegistration(long OwnerUid, DateTime Timer)
{
    public const int StatusFlag = 0;
    public const int Seconds = 30;
    public DateTime ExpiresAtLocal => Timer.AddSeconds(Seconds);

    public static DateTime LocalTime(TimeProvider provider, DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, provider.LocalTimeZone).DateTime;

    public static LegacyCanAttackFlashingRegistration Create(long ownerUid, TimeProvider provider) =>
        new(ownerUid, LocalTime(provider, provider.GetUtcNow()));

    internal bool IsValidFor(CharacterSnapshot? character) =>
        character is not null && character.Uid == OwnerUid &&
        LegacyPlayerDeathState.HasStatusFlag(character.Attributes, StatusFlag) &&
        Timer.Kind == DateTimeKind.Unspecified &&
        Timer.Ticks <= DateTime.MaxValue.Ticks - TimeSpan.TicksPerSecond * Seconds;

    public static bool TryRead(WorldSession session, out LegacyCanAttackFlashingRegistration registration)
    {
        registration = null!;
        if (session.CanAttackFlashingRegistration is not { } current ||
            !current.IsValidFor(session.Character)) return false;
        registration = current;
        return true;
    }

    public WorldSession Install(WorldSession session)
    {
        if (!IsValidFor(session.Character))
            throw new ArgumentException("Flashing registration requires its active original owner flag.", nameof(session));
        return session with { CanAttackFlashingRegistration = this };
    }

    public static WorldSession Forget(WorldSession session) =>
        session.CanAttackFlashingRegistration is null ? session :
            session with { CanAttackFlashingRegistration = null };
}
