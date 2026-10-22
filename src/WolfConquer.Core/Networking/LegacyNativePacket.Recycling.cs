using System.Collections.Concurrent;

namespace WolfConquer.Core.Networking;

internal static class LegacyNativePacketRecycle
{
    private static ConcurrentQueue<LegacyNativePacket> bin = new ConcurrentQueue<LegacyNativePacket>();

    public static int Count => bin.Count;

    public static LegacyNativePacket Take()
    {
        if (!bin.TryDequeue(out var result))
        {
            result = new LegacyNativePacket(16384);
        }
        result.Seek(4);
        return result;
    }

    public static void Reuse(LegacyNativePacket old)
    {
        old.Seek(0);
    }
}

internal class LegacyNativeRecycledPacket : IDisposable
{
    private LegacyNativePacket? stream;

    public LegacyNativePacket GetStream()
    {
        return stream!;
    }

    public LegacyNativeRecycledPacket()
    {
        stream = LegacyNativePacketRecycle.Take();
    }

    ~LegacyNativeRecycledPacket()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (stream != null)
        {
            LegacyNativePacketRecycle.Reuse(stream);
            stream = null;
            GC.SuppressFinalize(this);
        }
    }
}
