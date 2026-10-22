#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgNpc.Dialog owner, split at the independent Exists method only.
// This is not the gated Player.MessageBox owner or a host NPC dispatcher.
internal partial class LegacyNativeDialog
{
    private LegacyNativeGameClientRuntime client;
    public LegacyNativePacket stream;

    public LegacyNativeDialog(LegacyNativeGameClientRuntime Client, LegacyNativePacket _stream)
    {
        stream = _stream;
        client = Client;
    }

    public LegacyNativeDialog CreateMessageBox(string Text)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.MessageBox, Text, ushort.MaxValue, 0));
        return this;
    }

    public LegacyNativeDialog AddText(string text)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Dialog, text, 0, 0));
        return this;
    }

    public LegacyNativeDialog Text(string text)
    {
        return AddText(text);
    }

    public LegacyNativeDialog Option(string text, byte id)
    {
        return AddOption(text, id);
    }

    public LegacyNativeDialog Avatar(ushort id)
    {
        return AddAvatar(id);
    }

    public LegacyNativeDialog AddAvatar(ushort id)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Avatar, "", id, 0));
        return this;
    }

    public LegacyNativeDialog AddPage(string url)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Page, url, 0, byte.MaxValue));
        return this;
    }

    public LegacyNativeDialog AddOption(string text, byte id)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Option, text, 0, id));
        return this;
    }

    public LegacyNativeDialog AddOption(string text)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Option, text, 0, byte.MaxValue));
        return this;
    }

    public LegacyNativeDialog AddInput(string text, byte id, byte maxCharacters = 16)
    {
        client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Input, text, maxCharacters, id));
        return this;
    }

    public void FinalizeDialog(bool messagebox = false)
    {
        if (!messagebox)
        {
            client.Send(stream.NpcReplyCreate(LegacyNativeNpcReply.InteractTypes.Finish, "", 0, 0, display: false));
        }
    }
}
