using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyRefineEffect : byte
{
    None = 0,
    ToxinEraserLevel = 1,
    StrikeLockLevel = 2,
    LuckyStrike = 3,
    CalmWind = 4,
    DrainingTouch = 5,
    BloodSpawn = 6,
    LightOfStamina = 7,
    ShiledBreak = 8,
    KillingFlash = 9,
    MirrorOfSin = 0x0A,
    DivineGuard = 0x0B,
    CoreStrike = 0x0C,
    InvisbleArrow = 0x0D,
    FreeSoul = 0x0E,
    StraightLife = 0x0F,
    AbsoluteLuck = 0x10,
}

public readonly record struct LegacyMsgRefineEffectPacket(
    uint Id,
    uint Param,
    LegacyRefineEffect Effect)
{
    private const int IdField = 1;
    private const int ParamField = 2;
    private const int EffectField = 3;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgRefineEffect;

    public static LegacyMsgRefineEffectPacket KillingFlash(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.KillingFlash);
    }

    public static LegacyMsgRefineEffectPacket DrainingTouch(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.DrainingTouch);
    }

    public static LegacyMsgRefineEffectPacket BloodSpawn(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.BloodSpawn);
    }

    public static LegacyMsgRefineEffectPacket MirrorOfSin(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.MirrorOfSin);
    }

    public static LegacyMsgRefineEffectPacket LightOfStamina(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.LightOfStamina);
    }

    public static LegacyMsgRefineEffectPacket CalmWind(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.CalmWind);
    }

    public static LegacyMsgRefineEffectPacket DivineGuard(uint uid)
    {
        return new LegacyMsgRefineEffectPacket(uid, uid, LegacyRefineEffect.DivineGuard);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMsgRefineEffectPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var id = 0u;
        var param = 0u;
        var effect = LegacyRefineEffect.None;
        var hasId = false;
        var hasParam = false;
        var hasEffect = false;
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType) ||
                wireType != LegacyProtoWire.VarintWireType ||
                !reader.TryReadUInt32(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case IdField:
                    id = value;
                    hasId = true;
                    break;
                case ParamField:
                    param = value;
                    hasParam = true;
                    break;
                case EffectField:
                    if (value > byte.MaxValue)
                    {
                        return false;
                    }

                    effect = (LegacyRefineEffect)value;
                    hasEffect = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasId || !hasParam || !hasEffect)
        {
            return false;
        }

        packet = new LegacyMsgRefineEffectPacket(id, param, effect);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, IdField, Id);
        LegacyProtoWire.WriteVarintField(payload, ParamField, Param);
        LegacyProtoWire.WriteVarintField(payload, EffectField, (byte)Effect);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
