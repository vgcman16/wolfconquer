namespace WolfConquer.Core.Packets;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PacketHandlerAttribute : Attribute
{
    public PacketHandlerAttribute(ushort opcode)
    {
        Opcode = opcode;
    }

    public ushort Opcode { get; }
}
