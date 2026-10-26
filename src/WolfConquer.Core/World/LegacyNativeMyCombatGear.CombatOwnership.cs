namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private readonly LegacyNativeGameClientRuntime? combatConstructorOwner;

    // Physical binding only; no equipment-feature or mutation permission grant.
    internal bool IsCombatOwnedBy(LegacyNativeGameClientRuntime client) =>
        client is not null &&
        ReferenceEquals(combatConstructorOwner, client) && ReferenceEquals(user, client) &&
        ReferenceEquals(client.CombatGear, this);
}
