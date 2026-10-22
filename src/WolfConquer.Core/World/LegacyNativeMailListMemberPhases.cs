using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMailListMemberPhases
{
    // Complete native GameClient31 inline field initializer, explicit and unwired.
    // This does not reconstruct the full constructor or adopt LoadCharacter/SaveClient.
    internal static void AssignMailListAtNativeFieldInitializerSlot(LegacyNativeGameClientRuntime client)
    {
        client.MailList = new LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList>();
    }
}
