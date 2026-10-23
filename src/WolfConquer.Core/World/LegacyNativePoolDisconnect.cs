using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Native Pool102 physical initializer only; no worker/admission/cleanup route.
internal static class LegacyNativePoolDisconnect
{
    public static ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> DisconnectPool = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
}
