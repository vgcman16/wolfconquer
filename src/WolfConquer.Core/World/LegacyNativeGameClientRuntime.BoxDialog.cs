using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Whole native GameClient1554..1560 over the actual complete Dialog owner.
    public void CreateBoxDialog(string Text)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        LegacyNativeDialog dialog = new LegacyNativeDialog(this, stream);
        dialog.CreateMessageBox(Text).FinalizeDialog(messagebox: true);
    }
}
