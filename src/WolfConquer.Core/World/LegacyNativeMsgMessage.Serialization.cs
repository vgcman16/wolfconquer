#nullable disable

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgMessage
{
    public void Deserialize(LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var decoded = stream.ProtoBufferDeserialize(new MsgTalkProto());
        var content = decoded.Content;
        int length = content?.Length ?? 0;
        string from = length > 0 ? content[0] : _From;
        string to = length > 1 ? content[1] : _To;
        string message = length > 3 ? content[3] : __Message;
        string serverName = length > 6 ? content[6] : ServerName;
        uint color = decoded.Color, uid1 = decoded.MessageUID1, uid2 = decoded.MessageUID2;
        uint mesh = decoded.Mesh, new1 = decoded.New1, new2 = decoded.New2;
        var chatType = (ChatMode)decoded.ChatType;

        // Publish only the fully decoded/prepared state. Absent repeated
        // strings retain their previous slots and are never fabricated empty.
        proto = decoded;
        Color = color;
        ChatType = chatType;
        MessageUID1 = uid1;
        MessageUID2 = uid2;
        Mesh = mesh;
        New1 = new1;
        New2 = new2;
        _From = from;
        _To = to;
        __Message = message;
        ServerName = serverName;
    }

    public LegacyNativePacket GetArray(LegacyNativePacket stream, uint Rank = 0u)
    {
        stream.InitWriter();
        proto = new MsgTalkProto
        {
            Color = Color,
            ChatType = (uint)ChatType,
            MessageUID1 = MessageUID1,
            MessageUID2 = MessageUID2,
            Mesh = Mesh,
            UnionType = Rank,
            New1 = New1,
            New2 = New2,
            Count = 1u
        };
        if (ServerName != "")
        {
            uint serverID = 0u;
            foreach (LegacyNativeGroupServerList.Server value in LegacyNativeGroupServerList.GroupServers.Values)
            {
                if (value.Name == ServerName)
                {
                    proto.ServerID = serverID;
                    break;
                }
            }
        }
        proto.Content = new string[7]
        {
            _From,
            _To,
            string.Empty,
            __Message,
            string.Empty,
            string.Empty,
            ServerName
        };
        stream.ProtoBufferSerialize(proto);
        stream.Finalize(2179);
        return stream;
    }
}
