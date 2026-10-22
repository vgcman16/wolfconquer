#nullable disable

namespace WolfConquer.Core.World;

internal partial class LegacyNativeHundredWeapons
{
    public Dictionary<double, byte> GetWeaponRate(byte stage)
    {
        switch (stage)
        {
            case 1:
                if (TotalScore <= 1500)
                {
                    return new Dictionary<double, byte> { { 50.0, 15 } };
                }
                if (TotalScore >= 1501 && TotalScore <= 2000)
                {
                    return new Dictionary<double, byte> { { 55.0, 15 } };
                }
                if (TotalScore >= 2001 && TotalScore <= 2500)
                {
                    return new Dictionary<double, byte> { { 55.0, 15 } };
                }
                if (TotalScore >= 2501 && TotalScore <= 3000)
                {
                    return new Dictionary<double, byte> { { 60.0, 15 } };
                }
                if (TotalScore >= 3001 && TotalScore <= 5000)
                {
                    return new Dictionary<double, byte> { { 70.0, 15 } };
                }
                if (TotalScore >= 5001 && TotalScore <= 10000)
                {
                    return new Dictionary<double, byte> { { 80.0, 15 } };
                }
                if (TotalScore >= 10001 && TotalScore <= 15000)
                {
                    return new Dictionary<double, byte> { { 90.0, 15 } };
                }
                if (TotalScore >= 15001 && TotalScore <= 30000)
                {
                    return new Dictionary<double, byte> { { 100.0, 10 } };
                }
                if (TotalScore >= 30001 && TotalScore <= 40000)
                {
                    return new Dictionary<double, byte> { { 100.0, 8 } };
                }
                if (TotalScore >= 40001 && TotalScore <= 60000)
                {
                    return new Dictionary<double, byte> { { 100.0, 6 } };
                }
                if (TotalScore >= 60001 && TotalScore <= 80000)
                {
                    return new Dictionary<double, byte> { { 100.0, 4 } };
                }
                if (TotalScore >= 80001 && TotalScore <= 100000)
                {
                    return new Dictionary<double, byte> { { 100.0, 2 } };
                }
                if (TotalScore >= 100001 && TotalScore <= 130000)
                {
                    return new Dictionary<double, byte> { { 100.0, 1 } };
                }
                if (TotalScore >= 130001)
                {
                    return new Dictionary<double, byte> { { 100.0, 0 } };
                }
                break;
            case 2:
                if (TotalScore <= 1500)
                {
                    return new Dictionary<double, byte> { { 1.0, 50 } };
                }
                if (TotalScore >= 1501 && TotalScore <= 2000)
                {
                    return new Dictionary<double, byte> { { 1.5, 50 } };
                }
                if (TotalScore >= 2001 && TotalScore <= 2500)
                {
                    return new Dictionary<double, byte> { { 1.5, 50 } };
                }
                if (TotalScore >= 2501 && TotalScore <= 3000)
                {
                    return new Dictionary<double, byte> { { 2.0, 45 } };
                }
                if (TotalScore >= 3001 && TotalScore <= 5000)
                {
                    return new Dictionary<double, byte> { { 3.0, 40 } };
                }
                if (TotalScore >= 5001 && TotalScore <= 10000)
                {
                    return new Dictionary<double, byte> { { 4.0, 35 } };
                }
                if (TotalScore >= 10001 && TotalScore <= 15000)
                {
                    return new Dictionary<double, byte> { { 5.0, 30 } };
                }
                if (TotalScore >= 15001 && TotalScore <= 30000)
                {
                    return new Dictionary<double, byte> { { 10.0, 25 } };
                }
                if (TotalScore >= 30001 && TotalScore <= 40000)
                {
                    return new Dictionary<double, byte> { { 12.0, 20 } };
                }
                if (TotalScore >= 40001 && TotalScore <= 60000)
                {
                    return new Dictionary<double, byte> { { 15.0, 20 } };
                }
                if (TotalScore >= 60001 && TotalScore <= 80000)
                {
                    return new Dictionary<double, byte> { { 20.0, 20 } };
                }
                if (TotalScore >= 80001 && TotalScore <= 100000)
                {
                    return new Dictionary<double, byte> { { 25.0, 20 } };
                }
                if (TotalScore >= 100001 && TotalScore <= 130000)
                {
                    return new Dictionary<double, byte> { { 28.0, 20 } };
                }
                if (TotalScore >= 130001)
                {
                    return new Dictionary<double, byte> { { 30.0, 20 } };
                }
                break;
            case 3:
                if (TotalScore <= 1500)
                {
                    return new Dictionary<double, byte> { { 0.2, 90 } };
                }
                if (TotalScore >= 1501 && TotalScore <= 2000)
                {
                    return new Dictionary<double, byte> { { 0.2, 90 } };
                }
                if (TotalScore >= 2001 && TotalScore <= 2500)
                {
                    return new Dictionary<double, byte> { { 0.2, 85 } };
                }
                if (TotalScore >= 2501 && TotalScore <= 3000)
                {
                    return new Dictionary<double, byte> { { 0.4, 80 } };
                }
                if (TotalScore >= 3001 && TotalScore <= 5000)
                {
                    return new Dictionary<double, byte> { { 0.6, 70 } };
                }
                if (TotalScore >= 5001 && TotalScore <= 10000)
                {
                    return new Dictionary<double, byte> { { 0.8, 60 } };
                }
                if (TotalScore >= 10001 && TotalScore <= 15000)
                {
                    return new Dictionary<double, byte> { { 1.0, 55 } };
                }
                if (TotalScore >= 15001 && TotalScore <= 30000)
                {
                    return new Dictionary<double, byte> { { 1.2, 50 } };
                }
                if (TotalScore >= 30001 && TotalScore <= 40000)
                {
                    return new Dictionary<double, byte> { { 1.4, 45 } };
                }
                if (TotalScore >= 40001 && TotalScore <= 60000)
                {
                    return new Dictionary<double, byte> { { 1.6, 40 } };
                }
                if (TotalScore >= 60001 && TotalScore <= 80000)
                {
                    return new Dictionary<double, byte> { { 1.8, 35 } };
                }
                if (TotalScore >= 80001 && TotalScore <= 100000)
                {
                    return new Dictionary<double, byte> { { 2.0, 30 } };
                }
                if (TotalScore >= 100001 && TotalScore <= 130000)
                {
                    return new Dictionary<double, byte> { { 2.2, 30 } };
                }
                if (TotalScore >= 130001)
                {
                    return new Dictionary<double, byte> { { 2.5, 30 } };
                }
                break;
        }
        return null;
    }
}
