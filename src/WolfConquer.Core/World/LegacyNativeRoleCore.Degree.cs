using System;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
    // Entire native Core.GetDegree188..199. Int32 subtraction precedes widening.
    // Math.Atan2 sees finite Int32 operands, so the original Int32 cast is bounded
    // to 0..360. No general-purpose floating conversion policy is introduced.
    public static int GetDegree(int X, int X2, int Y, int Y2)
    {
        double x = X2 - X;
        double y = Y2 - Y;
        double num2 = Math.Atan2(y, x);
        if (num2 < 0.0)
        {
            num2 += Math.PI * 2.0;
        }
        return (int)(360.0 - num2 * 180.0 / Math.PI);
    }
}
