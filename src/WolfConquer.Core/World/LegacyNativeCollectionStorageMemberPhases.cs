namespace WolfConquer.Core.World;

internal static class LegacyNativeCollectionStorageMemberPhases
{
    // Native GameClient constructor1385, after Lottery and before CombatGear.
    // Explicit and unwired; this does not reconstruct the omitted full constructor.
    internal static void AssignCollectionStorageAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.CollectionStorage = new LegacyNativeCollectionStorageRuntime(client);
    }
}
