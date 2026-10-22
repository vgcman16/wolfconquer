namespace WolfConquer.Core.World;

internal static class LegacyNativeAnimaSkinMemberPhases
{
    // Native GameClient constructor1388: after MyNinja and before MyExchangeShop.
    // Explicit and unwired; this does not reconstruct the omitted full constructor.
    internal static void AssignAnimaSkinAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.MyAnimaSkin = new LegacyNativeAnimaSkinRuntime(client);
    }
}
