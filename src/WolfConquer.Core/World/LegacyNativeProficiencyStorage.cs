#nullable disable
using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Genuine native Proficiency field initializers, constructor and CheckProf. Native
// Add/UpdSpell/CheckUpdate/SendAll are absent, not stubbed. There is no
// DataCore, login, host or client-constructor caller for this owner.
internal partial class LegacyNativeProficiencyStorage
{
    public ConcurrentDictionary<uint, LegacyNativeMsgProficiency> ClientProf =
        new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>();

    private LegacyNativeGameClientRuntime Owner;

    public LegacyNativeProficiencyStorage(LegacyNativeGameClientRuntime _own)
    {
        Owner = _own;
    }
}
