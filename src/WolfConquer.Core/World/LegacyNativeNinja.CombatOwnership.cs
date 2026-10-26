namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private readonly LegacyNativeGameClientRuntime? combatConstructorOwner;

    // Parameterless storage remains unowned; rebinding user cannot invent birth.
    internal bool IsCombatOwnedBy(LegacyNativeGameClientRuntime client) =>
        client is not null &&
        ReferenceEquals(combatConstructorOwner, client) && ReferenceEquals(user, client) &&
        ReferenceEquals(client.MyNinja, this);
}
