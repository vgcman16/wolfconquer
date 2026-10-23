using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeAssociateRuntime
{
    public const byte Friends = 1;
    public const byte Enemy = 2;
    public const byte Partener = 3;
    public const byte Mentor = 4;
    public const byte Apprentice = 5;
    public const byte PKExplorer = 6;

    public static ConcurrentDictionary<uint, MyAsociats> Associates = new ConcurrentDictionary<uint, MyAsociats>();

    public static void RemoveOffline(byte Mode, uint UID, uint OnRemove)
    {
        if (Associates.TryGetValue(UID, out var value)) value.Remove(Mode, OnRemove);
    }
}
