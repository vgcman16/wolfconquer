using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyRaceItemType : ushort
{
    Null = 8329,
    ChaosBomb = 8330,
    SpiritPotion = 8331,
    ExcitementPotion = 8332,
    FrozenTrap = 8333,
    ScreamBomb = 8334,
    SluggishPotion = 8335,
    GuardPotion = 8336,
    DizzyHammer = 8337,
    TransformItem = 8338,
    RestorePotion = 8339,
    SuperExcitementPotion = 8340,
}

public readonly record struct LegacyRacePotionPacket(
    ushort Amount,
    LegacyRaceItemType PotionType,
    int Location,
    uint DwParam)
{
    private const int AmountOffset = 0;
    private const int PotionTypeOffset = AmountOffset + sizeof(ushort);
    private const int LocationOffset = PotionTypeOffset + sizeof(ushort);
    private const int DwParamOffset = LocationOffset + sizeof(int);

    public const ushort Opcode = WorldPacketOpcodes.LegacyRacePotion;
    public const int PayloadLength = DwParamOffset + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyRacePotionPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        Span<byte> payload = stackalloc byte[PayloadLength];
        var copied = 0;
        foreach (var memory in frame.Payload)
        {
            var span = memory.Span;
            span.CopyTo(payload[copied..]);
            copied += span.Length;
        }

        var potionType = BinaryPrimitives.ReadUInt16LittleEndian(payload[PotionTypeOffset..LocationOffset]);
        if (!Enum.IsDefined(typeof(LegacyRaceItemType), potionType))
        {
            return false;
        }

        packet = new LegacyRacePotionPacket(
            BinaryPrimitives.ReadUInt16LittleEndian(payload[AmountOffset..PotionTypeOffset]),
            (LegacyRaceItemType)potionType,
            BinaryPrimitives.ReadInt32LittleEndian(payload[LocationOffset..DwParamOffset]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[DwParamOffset..PayloadLength]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(AmountOffset, Amount);
        writer.WriteUInt16(PotionTypeOffset, (ushort)PotionType);
        writer.WriteInt32(LocationOffset, Location);
        writer.WriteUInt32(DwParamOffset, DwParam);
        return writer.ToArray();
    }
}
