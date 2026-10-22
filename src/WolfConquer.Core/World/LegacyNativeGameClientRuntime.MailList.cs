using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient31 inline-new field. Its explicit field-initializer phase below is
    // unwired; the present partial client constructor does not allocate the native mail catalog.
    internal LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList>? MailList;
}
