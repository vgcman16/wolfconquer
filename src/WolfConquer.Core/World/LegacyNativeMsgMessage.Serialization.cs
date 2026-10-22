#nullable disable

using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgMessage
{
    public void Deserialize(LegacyNativePacket stream)
    {
        proto = new MsgTalkProto();
        proto = stream.ProtoBufferDeserialize(proto);
        Color = proto.Color;
        ChatType = (ChatMode)proto.ChatType;
        MessageUID1 = proto.MessageUID1;
        MessageUID2 = proto.MessageUID2;
        Mesh = proto.Mesh;
        New1 = proto.New1;
        New2 = proto.New2;
        if (proto.Content.Length != 0)
        {
            _From = proto.Content[0];
        }
        if (proto.Content.Length > 1)
        {
            _To = proto.Content[1];
        }
        if (proto.Content.Length > 3)
        {
            __Message = proto.Content[3];
        }
        if (proto.Content.Length > 6)
        {
            ServerName = proto.Content[6];
        }
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
