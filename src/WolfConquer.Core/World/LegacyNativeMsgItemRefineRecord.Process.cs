using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgItemRefineRecord
{
    // Whole native private Process; it does not parse a query or inspect inventory.
    // No Packet attribute/host registration is installed by this prerequisite.
    private static void Process(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        ItemRefineRecord itemRefineRecord = new ItemRefineRecord();
        itemRefineRecord.Type = 0u;
        itemRefineRecord.Points = 54u;
        client.Send(stream.ItemRefineRecordCreate(itemRefineRecord));
    }
}
