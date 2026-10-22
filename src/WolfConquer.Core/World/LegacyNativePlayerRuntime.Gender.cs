namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    public byte GetGender
    {
        get
        {
            if (Body % 10 == 8)
            {
                return 0;
            }
            return 1;
        }
    }
}
