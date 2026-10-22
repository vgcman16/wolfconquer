using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native Pool100 physical reference. Native startup publication remains absent.
internal static class LegacyNativePoolClients
{
    public static ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? GamePoll;
}
