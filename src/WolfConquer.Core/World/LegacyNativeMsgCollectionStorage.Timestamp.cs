namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgCollectionStorage
{
    public static int Timestamp()
    {
        DateTime dateTime = new DateTime(1970, 1, 1);
        return unchecked((int)(DateTime.Now - dateTime).TotalSeconds);
    }

    public static uint Timestamp(uint expiration_date)
    {
        int num = Timestamp();
        uint result = 0u;
        if (num < expiration_date)
        {
            result = unchecked((uint)(expiration_date - num));
        }
        return result;
    }
}
