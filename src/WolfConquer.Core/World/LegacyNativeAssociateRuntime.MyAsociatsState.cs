using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
        public ConcurrentDictionary<byte, ConcurrentDictionary<uint, Member>> Associat = new ConcurrentDictionary<byte, ConcurrentDictionary<uint, Member>>();
        public ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> OnlineApprentice = new ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>();
        public uint Online = 0u;
        public LegacyNativeGameClientRuntime? MyClient;
        public uint MyUID = 0u;
        public uint Mentor_Stones = 0u;
        public uint Mentor_ExpBalls = 0u;
        public uint Mentor_Blessing = 0u;

        public MyAsociats(uint uid)
        {
            MyUID = uid;
        }
    }
}

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player925/927. No lookup, registration, imported DTO or lazy allocation.
    internal LegacyNativeAssociateRuntime.MyAsociats? MyMentor = null;
    internal LegacyNativeAssociateRuntime.MyAsociats? Associate;
}
