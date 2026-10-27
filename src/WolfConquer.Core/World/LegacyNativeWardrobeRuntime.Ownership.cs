namespace WolfConquer.Core.World;

internal partial class LegacyNativeWardrobeRuntime
{
    internal bool IsOwnedBy(LegacyNativeGameClientRuntime client) => client is not null &&
        ReferenceEquals(constructorOwner, client) && ReferenceEquals(Owner, client) &&
        ReferenceEquals(client.MyWardrobe, this);
}
