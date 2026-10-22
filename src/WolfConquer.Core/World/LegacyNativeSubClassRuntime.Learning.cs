#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    public bool LearnNewSubClass(LegacyNativeDbLevExp.Sort type, LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        return user.Player.SubClass.SendLearn(user, LegacyNativeMsgSubClass.SubClases.Create(type), stream);
    }

    public byte GetClassLevel(LegacyNativeDbLevExp.Sort type)
    {
        if (src.TryGetValue(type, out var value))
        {
            return value.Level;
        }
        return 0;
    }

    public byte GetClassPhrase(LegacyNativeDbLevExp.Sort type)
    {
        if (src.TryGetValue(type, out var value))
        {
            return value.Phrase;
        }
        return 0;
    }

    public bool SendLearn(LegacyNativeGameClientRuntime client, LegacyNativeMsgSubClass.SubClases item, LegacyNativePacket stream)
    {
        if (!src.ContainsKey(item.ID))
        {
            src.TryAdd(item.ID, item);
            client.Send(stream.SubClassCreate(LegacyNativeMsgSubClass.Action.LearnSubClass, item.ID, item.Level, 0));
            return true;
        }
        return false;
    }

    public void SendPromoted(LegacyNativeGameClientRuntime client, LegacyNativeMsgSubClass.SubClases item, LegacyNativePacket stream)
    {
        client.Send(stream.SubClassCreate(LegacyNativeMsgSubClass.Action.MartialPromoted, item.ID, item.Phrase, 0));
    }

    public void ShowGui(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        client.Send(stream.SubClassCreate(LegacyNativeMsgSubClass.Action.ShowGUI, StudyPoints, 0, src.Values.ToArray()));
    }
}
