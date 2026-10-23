namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player1637..1648. Reads the genuine Class accessor only;
    // the full Class setter is separately held, not replaced by a cold writer.
    public uint LoweredClass
    {
        get
        {
            uint num = Class;
            while (num % 100 > 5)
            {
                num--;
            }
            return num;
        }
    }
}
