#nullable disable
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Complete native Proficiency field initializers, constructor and methods.
// Packet codecs and thresholds retain their genuine owners. Ordinary admission
// prepares this constructor-bound owner from the actual saved session; no lazy
// operation creates an accepting fallback or a native save worker.
internal partial class LegacyNativeProficiencyStorage
{
    public ConcurrentDictionary<uint, LegacyNativeMsgProficiency> ClientProf =
        new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>();

    private LegacyNativeGameClientRuntime Owner;

    public LegacyNativeProficiencyStorage(LegacyNativeGameClientRuntime _own)
    {
        ArgumentNullException.ThrowIfNull(_own);
        Owner = _own;
    }
}
