namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    // Preserve the native void entry ABI and caught diagnostic, while replacing
    // early readiness/partial publication with complete temporary preparation.
    public static void Load(string DBLocation)
    {
        try { MailboxLoadProof.LoadAndPublish(DBLocation); }
        catch (Exception error) { Console.WriteLine(error); }
    }
}
