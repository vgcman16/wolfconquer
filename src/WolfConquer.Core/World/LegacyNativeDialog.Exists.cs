using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeDialog
{
    // Whole genuine network-capable native method. No caller or SQL execution is installed.
    // The native builder's identity "escape" is unsafe for untrusted input; do not
    // expose this through a host until a separate parameterized boundary exists.
    public static bool Exists(string username)
    {
        try
        {
            using LegacyNativeMySqlCommand command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT)
                .Select("accounts").Where("Username", username);
            using LegacyNativeMySqlReader mySqlReader = new LegacyNativeMySqlReader(command);
            if (mySqlReader.Read())
            {
                return true;
            }
        }
        catch (Exception value)
        {
            Console.WriteLine(value);
        }
        return false;
    }
}
