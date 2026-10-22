#nullable disable

namespace WolfConquer.Core.World;

// Native MsgMessage physical owner, all declaration initializers and all six
// constructors. MsgHandler/ChatCommands and host packet registration are absent.
internal partial class LegacyNativeMsgMessage
{
    public MsgTalkProto proto;

    public string _From;

    public string _To;

    public ChatMode ChatType;

    public uint Color;

    public string __Message;

    public string ServerName = string.Empty;

    public uint New1;

    public uint New2;

    public uint Mesh;

    public uint MessageUID1 = 0u;

    public uint MessageUID2 = 0u;

    public static uint TestGui = 0u;

    public static object commandsFile = new object();

    public LegacyNativeMsgMessage(string _Message, MsgColor _Color, ChatMode _ChatType)
    {
        Mesh = 0u;
        __Message = _Message;
        _To = "ALL";
        _From = "SYSTEM";
        Color = (uint)_Color;
        ChatType = _ChatType;
    }

    public LegacyNativeMsgMessage(string _Message, string __To, MsgColor _Color, ChatMode _ChatType)
    {
        Mesh = 0u;
        __Message = _Message;
        _To = __To;
        _From = "SYSTEM";
        Color = (uint)_Color;
        ChatType = _ChatType;
    }

    public LegacyNativeMsgMessage(string _Message, string __To, string __From, MsgColor _Color, ChatMode _ChatType)
    {
        Mesh = 0u;
        __Message = _Message;
        _To = __To;
        _From = __From;
        Color = (uint)_Color;
        ChatType = _ChatType;
    }

    public LegacyNativeMsgMessage(string _Message, uint Mesh, string __To, string __From, MsgColor _Color, ChatMode _ChatType)
    {
        this.Mesh = Mesh;
        __Message = _Message;
        _To = __To;
        _From = __From;
        Color = (uint)_Color;
        ChatType = _ChatType;
    }

    public LegacyNativeMsgMessage(MsgColor color, ChatMode chat, uint uid1, uint uid2,
        uint new1, uint new2, string from, string to, string message)
    {
        Mesh = 0u;
        Color = (uint)color;
        ChatType = chat;
        MessageUID1 = uid1;
        MessageUID2 = uid2;
        New1 = new1;
        New2 = new2;
        _From = from;
        _To = to;
        __Message = message;
    }

    public LegacyNativeMsgMessage()
    {
        Mesh = 0u;
    }
}
