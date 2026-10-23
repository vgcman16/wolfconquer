namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    internal bool IsOwnedBy(LegacyNativeGameClientRuntime client) =>
        client is not null && ReferenceEquals(Owner, client);
}
