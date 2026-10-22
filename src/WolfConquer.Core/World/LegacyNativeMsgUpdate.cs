using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native MsgUpdate333..385 and its mutable MsgBuilder236..269 message owners.
// Typed serialization is separate; this does not install Player.SendUpdate or transport binding.
internal partial class LegacyNativeMsgUpdate
{
    public class UpdateProto
    {
        public uint UID;

        public UpdateQuery[]? Updates;
    }

    public class UpdateQuery
    {
        public DataType Type;

        public ulong Value;

        public ulong[]? Values;

        public uint Flag;

        public uint Time;

        public uint Damage;

        public uint SpellLevel;
    }

    public UpdateProto Info;

    public List<UpdateQuery> myUpdates;

    public LegacyNativeMsgUpdate(LegacyNativePacket stream, uint UID, int count = 1)
    {
        Info = new UpdateProto
        {
            UID = UID,
        };
        myUpdates = new List<UpdateQuery>();
    }

    public LegacyNativePacket Append(LegacyNativePacket stream, DataType ID, long Value)
    {
        myUpdates.Add(new UpdateQuery
        {
            Type = ID,
            Value = unchecked((ulong)Value),
        });
        return stream;
    }

    public LegacyNativePacket Append(LegacyNativePacket stream, DataType ID,
        uint Flag, uint Time, uint Dmg, uint Level)
    {
        myUpdates.Add(new UpdateQuery
        {
            Type = ID,
            Flag = Flag,
            Time = Time,
            Damage = Dmg,
            SpellLevel = Level,
        });
        return stream;
    }

    public LegacyNativePacket Append(LegacyNativePacket stream, DataType ID, uint[] Value)
    {
        ulong[] array = new ulong[Value.Length];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = Value[i];
        }
        myUpdates.Add(new UpdateQuery
        {
            Type = ID,
            Values = array,
        });
        return stream;
    }

    public LegacyNativePacket GetArray(LegacyNativePacket stream)
    {
        Info.Updates = myUpdates.ToArray();
        SerializeTypedUpdateProtoIntoPacket(stream, Info);
        stream.Finalize(2176);
        return stream;
    }
}
