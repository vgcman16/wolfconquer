namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Entire native Player2715..2725, through the genuine whole Hair property.
    public byte HairColor
    {
        get { return (byte)(Hair / 1000); }
        set { Hair = (ushort)(value * 1000 + Hair % 1000); }
    }
}
