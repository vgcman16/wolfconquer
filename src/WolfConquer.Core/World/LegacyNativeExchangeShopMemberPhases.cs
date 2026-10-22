namespace WolfConquer.Core.World;

internal static class LegacyNativeExchangeShopMemberPhases
{
    // Native GameClient constructor1389: after MyAnimaSkin and before MyWardrobe.
    // Explicit and unwired; this is not a replacement full native constructor.
    internal static void AssignExchangeShopAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.MyExchangeShop = new LegacyNativeExchangeShopRuntime(client);
    }
}
