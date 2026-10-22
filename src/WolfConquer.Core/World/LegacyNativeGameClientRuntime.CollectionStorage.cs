namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient27 physical reference. Constructor1385 owns assignment;
    // the present partial client birth intentionally does not initialize it.
    internal LegacyNativeCollectionStorageRuntime? CollectionStorage;
}
