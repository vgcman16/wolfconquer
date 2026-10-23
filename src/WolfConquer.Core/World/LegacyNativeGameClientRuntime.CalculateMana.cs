namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Complete native GameClient1694..1715, literal class ranges and narrowing.
    public ushort CalculateMana()
    {
        ushort num = 0;
        if ((Player.Class >= 13005 && Player.Class <= 13051) || (Player.Class >= 14005 && Player.Class <= 14051))
        {
            return (ushort)(num + (ushort)(Player.Spirit * 30));
        }
        switch (Player.Class)
        {
            case 13002u:
            case 14002u:
                return (ushort)(num + (ushort)(Player.Spirit * 15));
            case 13003u:
            case 14003u:
                return (ushort)(num + (ushort)(Player.Spirit * 20));
            case 13004u:
            case 14004u:
                return (ushort)(num + (ushort)(Player.Spirit * 25));
            default:
                return (ushort)(num + (ushort)(Player.Spirit * 5));
        }
    }
}
