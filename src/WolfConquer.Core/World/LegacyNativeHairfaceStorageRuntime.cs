using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Native constructor and saved-storage methods. Gameplay mutation/send methods remain separate.
internal partial class LegacyNativeHairfaceStorageRuntime
{
    private List<LegacyNativeHairfaceStorageTable.Hairface> items;

    private LegacyNativeHairfaceStorageTable.Hairface[] objects;

    private readonly LegacyNativeGameClientRuntime Owner;

    public LegacyNativeHairfaceStorageTable.Hairface[] Objects => objects;

    public LegacyNativeHairfaceStorageRuntime(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        Owner = client;
        items = new List<LegacyNativeHairfaceStorageTable.Hairface>();
        objects = new LegacyNativeHairfaceStorageTable.Hairface[0];
    }
}
