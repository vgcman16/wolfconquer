using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Entire native Role.Instance.Mail, distinct from the ElTabakh.Mail packet codec.
internal class LegacyNativeRoleMailRuntime
{
    public ConcurrentDictionary<uint, LegacyNativeMsgProficiency> Mails = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>();

    private LegacyNativeGameClientRuntime Owner;

    public LegacyNativeRoleMailRuntime(LegacyNativeGameClientRuntime _own)
    {
        Owner = _own;
    }
}
