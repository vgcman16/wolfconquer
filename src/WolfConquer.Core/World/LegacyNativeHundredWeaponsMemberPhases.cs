namespace WolfConquer.Core.World;

internal static class LegacyNativeHundredWeaponsMemberPhases
{
    // Complete native LoadCharacter1106..1109 conditional birth, explicitly unwired.
    // This does not stand in for the full LoadCharacter function or perform SQL/file loading.
    internal static void AssignHundredWeaponsAtNativeLoadCharacterSlot(LegacyNativeGameClientRuntime client)
    {
        if (client.HundredWeapons == null)
        {
            client.HundredWeapons = new LegacyNativeHundredWeapons(client);
        }
    }
}
