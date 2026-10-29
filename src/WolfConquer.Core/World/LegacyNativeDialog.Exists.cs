using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeDialog
{
    // SQL-capable owner only; no host caller is installed here.
    // An absent row is a valid lookup result; failed lookup must propagate.
    public static bool Exists(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        using LegacyNativeMySqlCommand command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT)
            .Select("accounts").Where("Username", username);
        using LegacyNativeMySqlReader mySqlReader = new LegacyNativeMySqlReader(command);
        return mySqlReader.Read();
    }
}
