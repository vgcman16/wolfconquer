namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Native Role.Core139..164. Differences narrow to signed16 BEFORE comparison;
    // this is deliberately not the host's ordinary integer Chebyshev distance.
    internal static short GetDistance(ushort X, ushort Y, ushort X2, ushort Y2)
    {
        short num = 0;
        short num2 = 0;
        if (X >= X2)
        {
            num = (short)(X - X2);
        }
        else if (X2 >= X)
        {
            num = (short)(X2 - X);
        }
        if (Y >= Y2)
        {
            num2 = (short)(Y - Y2);
        }
        else if (Y2 >= Y)
        {
            num2 = (short)(Y2 - Y);
        }
        if (num > num2)
        {
            return num;
        }
        return num2;
    }
}
