namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeRuneItemStorage
{
    // Physical constructor/client-slot ownership, not an equipped-effect or mutation grant.
    internal bool IsSaveOwnedBy(LegacyNativeGameClientRuntime client) =>
        ReferenceEquals(constructorOwner, client) && ReferenceEquals(Owner, client) &&
        ReferenceEquals(client.Rune, this);
}
