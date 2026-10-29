using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeDialog
{
    // Whole genuine network-capable native method. No caller or SQL execution is installed.
    // Username is bound by the parameterized builder; host activation remains separate.
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
