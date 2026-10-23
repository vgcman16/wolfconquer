using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear551..662. Plus/Super deliberately ignore Size.
    public static uint GetExp(TypeExp type, uint ID = 0u, uint Size = 0u)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return 0u;
        uint num = 0u;
        switch (type)
        {
            case TypeExp.item:
                switch (ID)
                {
                    case 730001u: num += 40 * Size; break;
                    case 730002u: num += 160 * Size; break;
                    case 730003u: num += 400 * Size; break;
                    case 730004u: num += 1440 * Size; break;
                    case 730005u: num += 4320 * Size; break;
                    case 730006u: num += 12960 * Size; break;
                    case 730007u: num += 38880 * Size; break;
                    case 730008u: num += 116640 * Size; break;
                    case 3333400u: num += 120 * Size; break;
                    case 3333401u: num += 480 * Size; break;
                    case 3333402u: num += 1440 * Size; break;
                    case 3333403u: num += 4320 * Size; break;
                    case 3335125u: num += 120 * Size; break;
                    case 3335126u: num += 480 * Size; break;
                    case 3335127u: num += 1440 * Size; break;
                    case 3335128u: num += 4320 * Size; break;
                }
                return num;
            case TypeExp.Plus:
                switch (ID)
                {
                    case 1u: num += 40; break;
                    case 2u: num += 160; break;
                    case 3u: num += 400; break;
                    case 4u: num += 1440; break;
                    case 5u: num += 4320; break;
                    case 6u: num += 12960; break;
                    case 7u: num += 38880; break;
                    case 8u: num += 116640; break;
                }
                return num;
            case TypeExp.Super:
                switch ((byte)(ID % 10))
                {
                    case 9: num += 20; break;
                    case 8: num += 6; break;
                    case 7: num += 2; break;
                    case 6: num++; break;
                }
                return num;
            default:
                return num;
        }
    }

    // Complete native1129..1229, including d1's shared upper bound and signed overflow.
    public static int GetDamage(int damage, int d1, int d2, int d3, int d4, int d5, bool Player)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
            return 0;
        int num = damage;
        int num2 = 0;
        int num3 = 0;
        int num4 = 0;
        int num5 = 0;
        int num6 = 0;
        int num7 = 0;
        if (d1 > 30000 && d1 <= 31000)
            num2 = d1 % 1000;
        if (d2 > 30000 && d1 <= 31000)
            num3 = d2 % 1000;
        if (d3 > 30000 && d1 <= 31000)
            num4 = d3 % 1000;
        if (d4 > 30000 && d1 <= 31000)
            num5 = d4 % 1000;
        if (d5 > 30000 && d1 <= 31000)
            num6 = d5 % 1000;
        if (num2 != 0 && num7 == 0)
            num7 = num2;
        if (num3 != 0 && num7 == 0)
            num7 = num3;
        if (num4 != 0 && num7 == 0)
            num7 = num4;
        if (num5 != 0 && num7 == 0)
            num7 = num5;
        if (num6 != 0 && num7 == 0)
            num7 = num6;
        if (Player)
        {
            if (num7 > num2) num7 = num2;
            if (num7 > num3) num7 = num3;
            if (num7 > num4) num7 = num4;
            if (num7 > num5) num7 = num5;
            if (num7 > num6) num7 = num6;
        }
        else
        {
            if (num7 < num2) num7 = num2;
            if (num7 < num3) num7 = num3;
            if (num7 < num4) num7 = num4;
            if (num7 < num5) num7 = num5;
            if (num7 < num6) num7 = num6;
        }
        return num += num * num7 / 100;
    }
}
