namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
    // Complete native ItemType1826..2239, grouping only identical family branches.
    // Read metadata.Class once; retain upper-bound then lower-bound Player reads.
    public static bool EquipPassJobReq(LegacyNativeItemRuneMetadata baseInformation, LegacyNativeGameClientRuntime client)
    {
        uint requiredClass = baseInformation.Class;
        switch (requiredClass)
        {
            case 1000u or 1001u or 1002u or 1003u or 1004u or 1005u:
                return client.Player.Class <= 1051 && client.Player.Class >= requiredClass;
            case 2000u or 2001u or 2002u or 2003u or 2004u or 2005u:
                return client.Player.Class <= 2051 && client.Player.Class >= requiredClass;
            case 4000u or 4001u or 4002u or 4003u or 4004u or 4005u:
                return client.Player.Class <= 4051 && client.Player.Class >= requiredClass;
            case 5000u or 5001u or 5002u or 5003u or 5004u or 5005u:
                return client.Player.Class <= 5051 && client.Player.Class >= requiredClass;
            case 6000u or 6001u or 6002u or 6003u or 6004u or 6005u:
                return client.Player.Class <= 6051 && client.Player.Class >= requiredClass;
            case 7000u or 7001u or 7002u or 7003u or 7004u or 7005u:
                return client.Player.Class <= 7051 && client.Player.Class >= requiredClass;
            case 8000u or 8001u or 8002u or 8003u or 8004u or 8005u:
                return client.Player.Class <= 8049 && client.Player.Class >= requiredClass;
            case 9000u or 9001u or 9002u or 9003u or 9004u or 9005u:
                return client.Player.Class <= 9051 && client.Player.Class >= requiredClass;
            case 13000u or 13001u or 13002u or 13003u or 13004u or 13005u:
                return client.Player.Class <= 13051 && client.Player.Class >= requiredClass;
            case 14000u or 14001u or 14002u or 14003u or 14004u or 14005u:
                return client.Player.Class <= 14051 && client.Player.Class >= requiredClass;
            case 10000u:
            case 19000u:
                return client.Player.Class >= 10000;
            case 16000u or 16001u or 16002u or 16003u or 16004u or 16005u:
                return client.Player.Class <= 16051 && client.Player.Class >= requiredClass;
            case 0u:
                return true;
            default:
                return false;
        }
    }
}
