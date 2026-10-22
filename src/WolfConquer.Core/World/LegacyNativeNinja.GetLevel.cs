#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void GetLevel()
    {
        byte b = 0;
        if (user.MyNinja.Levels >= 17)
        {
            b = 9;
        }
        else if (user.MyNinja.Levels >= 16)
        {
            b = 8;
        }
        else if (user.MyNinja.Levels >= 15)
        {
            b = 7;
        }
        else if (user.MyNinja.Levels >= 14)
        {
            b = 6;
        }
        else if (user.MyNinja.Levels >= 13)
        {
            b = 5;
        }
        else if (user.MyNinja.Levels >= 12)
        {
            b = 4;
        }
        else if (user.MyNinja.Levels >= 11)
        {
            b = 3;
        }
        else if (user.MyNinja.Levels >= 10)
        {
            b = 2;
        }
        else if (user.MyNinja.Levels >= 9)
        {
            b = 1;
        }
        if (user.Player.NiniaP0 != b)
        {
            user.Player.NiniaP0 = b;
        }
    }
}
