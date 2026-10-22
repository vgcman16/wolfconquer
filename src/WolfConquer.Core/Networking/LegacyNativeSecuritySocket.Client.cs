namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeSecuritySocket
{
    // Native SecuritySocket26; directly stores the real owning client/pipe reference.
    public object? Client;
}
