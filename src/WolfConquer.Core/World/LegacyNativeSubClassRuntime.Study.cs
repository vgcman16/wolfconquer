using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    public void AddStudyPoints(LegacyNativeGameClientRuntime client, ushort Study, LegacyNativePacket stream)
    {
        StudyPoints = (ushort)Math.Min(65535, StudyPoints + Study);
        client.SendSysMesage("You received " + Study + " StudyPoints.", LegacyNativeMsgMessage.ChatMode.Talk);
        UpdateStudyPacket(client, stream);
    }

    public void RemoveStudy(LegacyNativeGameClientRuntime client, ushort Study, LegacyNativePacket stream)
    {
        if (StudyPoints > Study)
        {
            StudyPoints -= Study;
            UpdateStudyPacket(client, stream);
        }
    }

    public void UpdateStudyPacket(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        client.Player.SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, true, "zf2-e300");
        client.Send(stream.SubClassCreate(LegacyNativeMsgSubClass.Action.Animation, StudyPoints, 0));
    }
}
