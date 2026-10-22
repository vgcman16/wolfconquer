#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpellAnimation
{
    // Full native [ProtoContract] declarations. Attribute facts are recorded here without
    // introducing a Core dependency on the pinned original generic protobuf implementation.
    public class SpellUseProto
    {
        public uint Attacker;       // ProtoMember1 required
        public uint Attacked;       // ProtoMember2 optional
        public ushort X;            // ProtoMember3 optional
        public ushort Y;            // ProtoMember4 optional
        public ushort SpellID;      // ProtoMember5 required
        public byte SpellLevel;     // ProtoMember6 required
        public byte LevelHu;        // ProtoMember8 required (native declaration order)
        public ushort NextSpell;    // ProtoMember7 required
        public byte SpellEffect;    // ProtoMember9 required
        public Target[] Targets;    // ProtoMember10 optional
    }

    public class Target
    {
        public uint UID;            // ProtoMember1 required
        public uint Damage;         // ProtoMember2 required
        public bool Hit;            // ProtoMember3 required
        public uint Flag;           // ProtoMember4 required, NOT an enum contract
        public ushort newX;         // ProtoMember6 required
        public ushort newY;         // ProtoMember7 required
        public uint Unknown;        // ProtoMember8 required
        public uint TailedBeast;    // ProtoMember9 required
    }

    // Concrete attributed-contract projection only, not generic ProtoBufferSerialize or metadata
    // registration. Native unsafe copy/cursor behavior is retained; callers must bound allocations.
    private static unsafe void SerializeTypedSpellUseProtoIntoPacket(LegacyNativePacket stream, SpellUseProto value)
    {
        if (stream is null)
            throw new NullReferenceException();
        using MemoryStream output = new MemoryStream();
        SerializeTypedSpellUseProto(output, value);
        byte[] array = output.ToArray();
        fixed (byte* src = array)
            stream.memcpy(stream.Memory + stream.Position, src, array.Length);
        stream.SeekForward(array.Length);
    }

    private static void SerializeTypedSpellUseProto(Stream output, SpellUseProto value)
    {
        if (value is null)
            return;
        Field(output, 1, value.Attacker);
        if (value.Attacked != 0) Field(output, 2, value.Attacked);
        if (value.X != 0) Field(output, 3, value.X);
        if (value.Y != 0) Field(output, 4, value.Y);
        Field(output, 5, value.SpellID);
        Field(output, 6, value.SpellLevel);
        Field(output, 7, value.NextSpell);
        Field(output, 8, value.LevelHu);
        Field(output, 9, value.SpellEffect);
        if (value.Targets is null)
            return;
        foreach (Target item in value.Targets)
        {
            // Pinned protobuf array contracts omit null entries, rather than emitting empty rows.
            if (item is null)
                continue;
            using MemoryStream nested = new MemoryStream();
            Field(nested, 1, item.UID);
            Field(nested, 2, item.Damage);
            Field(nested, 3, item.Hit ? 1u : 0u);
            Field(nested, 4, item.Flag);
            Field(nested, 6, item.newX);
            Field(nested, 7, item.newY);
            Field(nested, 8, item.Unknown);
            Field(nested, 9, item.TailedBeast);
            byte[] bytes = nested.ToArray();
            Varint(output, (10u << 3) | 2u);
            Varint(output, (ulong)bytes.Length);
            output.Write(bytes, 0, bytes.Length);
        }
    }

    private static void Field(Stream output, uint field, ulong value)
    {
        Varint(output, (ulong)field << 3);
        Varint(output, value);
    }

    private static void Varint(Stream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)value);
    }
}
