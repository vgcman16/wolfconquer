namespace WolfConquer.Core.World;

internal partial class LegacyNativeProficiencyStorage
{
    internal bool IsOwnedBy(LegacyNativeGameClientRuntime client) =>
        client is not null && ReferenceEquals(Owner, client) && ReferenceEquals(client.MyProfs, this);
}
