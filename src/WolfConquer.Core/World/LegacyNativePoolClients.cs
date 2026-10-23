using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Native Pool100/104 physical references. Native startup publication remains absent.
internal static class LegacyNativePoolClients
{
    public static ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? GamePoll;

    public static List<int>? NameUsed;
}
