using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1163/1215/1217: separate physical backing fields, implicit zero.
    private ushort face;

    private ushort body;

    private ushort _transformationid;

    // Native Player2695..2713: unlike Body, Face sends to the owner only.
    public ushort Face
    {
        get
        {
            return face;
        }
        set
        {
            face = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    SendUpdate(stream, Mesh, LegacyNativeMsgUpdate.DataType.Mesh);
                }
            }
        }
    }

    // Native Player2806..2824: commit first, then owner send followed by view send.
    public ushort Body
    {
        get
        {
            return body;
        }
        set
        {
            body = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    SendUpdate(stream, Mesh, LegacyNativeMsgUpdate.DataType.Mesh, scren: true);
                }
            }
        }
    }

    // Native Player2826..2844; retained as a genuine Mesh input, not an appearance DTO.
    public ushort TransformationID
    {
        get
        {
            return _transformationid;
        }
        set
        {
            _transformationid = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    SendUpdate(stream, Mesh, LegacyNativeMsgUpdate.DataType.Mesh, scren: true);
                }
            }
        }
    }

    public uint Mesh => unchecked((uint)(TransformationID * 10000000 + Face * 10000 + Body));
}
