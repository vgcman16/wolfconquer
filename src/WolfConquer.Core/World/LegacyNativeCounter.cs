namespace WolfConquer.Core.World;

// System.Counter: plain shared-owner arithmetic, deliberately NOT synchronized.
public class LegacyNativeCounter
{
    private uint _value;

    public uint Next => unchecked(++_value);

    public uint Count => _value;

    public LegacyNativeCounter(uint start)
    {
        _value = start;
    }

    public void Set(uint start)
    {
        _value = start;
    }
}
