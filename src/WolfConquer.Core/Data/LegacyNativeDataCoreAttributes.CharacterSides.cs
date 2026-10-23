using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeDataCoreAttributes
{
    // Entire native DataCore11..78. Real Face setter/RNG behavior remains in place.
    public static void SetCharacterSides(LegacyNativePlayerRuntime Player)
    {
        byte b = (byte)(Player.Class / 1000);
        switch (Player.Body)
        {
        case 1008:
            switch (b)
            {
            case 1:
            case 2:
            case 4:
            case 10:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(1, 102);
                break;
            case 5:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(103, 107);
                break;
            case 6:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(109, 133);
                break;
            case 7:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(154, 158);
                break;
            case 8:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(164, 168);
                break;
            case 16:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(174, 178);
                break;
            default:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(1, 50);
                break;
            }
            break;
        case 2007:
            switch (b)
            {
            case 1:
            case 2:
            case 4:
            case 10:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(201, 290);
                break;
            case 5:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(291, 295);
                break;
            case 6:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(300, 304);
                break;
            case 7:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(345, 349);
                break;
            case 8:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(355, 359);
                break;
            case 16:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(365, 369);
                break;
            default:
                Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(201, 250);
                break;
            }
            break;
        default:
            Player.Face = (ushort)LegacyNativePoolRandom.GetRandom.Next(1, 50);
            break;
        }
    }
}
