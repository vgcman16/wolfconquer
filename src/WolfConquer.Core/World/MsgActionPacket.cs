using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum MsgActionType : ushort
{
    SetLocation = 74,
    Hotkeys = 75,
    ConfirmAssociates = 76,
    ConfirmProficiencies = 77,
    ConfirmSpells = 78,
    ChangeDirection = 79,
    ChangeStance = 81,
    ChangeMap = 85,
    Teleport = 86,
    Revive = 94,
    SetPkMode = 96,
    ConfirmGuild = 97,
    RequestEntity = 102,
    CancelFocusedSpell = 103,
    LegacyColor = 104,
    TeamSearchForMember = 106,
    StartVendor = 111,
    StopVending = 114,
    ViewEquipment = 117,
    FinishTransform = 118,
    EndFly = 120,
    OpenClientWindow = 126,
    CompleteLogin = 132,
    MonsterRespawnEffect = 134,
    RemoveEntity = 135,
    Jump = 137,
    Ghost = 145,
    LegacyMerchantStartup = 157,
    OpenGuiNpc = 160,
    Away = 161,
    ActionPick = 164,
    AllowAnimation = 251,
    UpdateInventorySash = 256,
    QuerySpawn = 310,
    QueryEquipment = 408,
    PetAttack = 447,
    ChangeLookface = 151,
    LegacyUnregistered476 = 476,
}

public sealed record MsgActionPacket
{
    private const int ObjIdField = 1;
    private const int TargetUidField = 2;
    private const int DwParamField = 3;
    private const int DwParam2Field = 4;
    private const int DwParam3Field = 5;
    private const int SucDoneField = 6;
    private const int TargetPositionXField = 7;
    private const int TargetPositionYField = 8;
    private const int TimestampField = 9;
    private const int NpcIdField = 10;
    private const int CheatLogData1Field = 11;
    private const int TypeField = 12;
    private const int FacingField = 13;
    private const int PositionXField = 14;
    private const int PositionYField = 15;
    private const int CheatLogData2Field = 16;
    private const int MapIdField = 17;
    private const int CheatLogData3Field = 18;
    private const int ScheduleTimeField = 19;
    private const int LinkIndexField = 20;
    private const int CheatLogData4Field = 21;
    private const int RemainTimeField = 22;
    private const int MountChargeField = 23;
    private const int StringsField = 24;
    private const int VarintWireType = 0;
    private const int Fixed64WireType = 1;
    private const int LengthDelimitedWireType = 2;
    private const int Fixed32WireType = 5;

    public const ushort Opcode = WorldPacketOpcodes.MsgAction;

    public uint ObjId { get; init; }

    public uint TargetUid { get; init; }

    public uint DwParam { get; init; }

    public int DwParam2 { get; init; }

    public long DwParam3 { get; init; }

    public bool SucDone { get; init; }

    public uint TargetPositionX { get; init; }

    public uint TargetPositionY { get; init; }

    public uint Timestamp { get; init; }

    public int NpcId { get; init; }

    public uint CheatLogData1 { get; init; }

    public MsgActionType Type { get; init; }

    public ushort Facing { get; init; }

    public uint PositionX { get; init; }

    public uint PositionY { get; init; }

    public uint CheatLogData2 { get; init; }

    public uint MapId { get; init; }

    public uint CheatLogData3 { get; init; }

    public int ScheduleTime { get; init; }

    public int LinkIndex { get; init; }

    public uint CheatLogData4 { get; init; }

    public int RemainTime { get; init; }

    public bool MountCharge { get; init; }

    public IReadOnlyList<string> Strings { get; init; } = [];

    public IReadOnlyList<uint> UnknownFields { get; init; } = [];

    public static MsgActionPacket CreateLegacyMerchantStartup(uint uid)
    {
        return new MsgActionPacket
        {
            ObjId = uid,
            DwParam = 2,
            Timestamp = 0,
            Type = MsgActionType.LegacyMerchantStartup,
            Facing = 0,
            PositionX = 0,
            PositionY = 0,
        };
    }

    public static bool TryParse(PacketFrame frame, out MsgActionPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var result = new MsgActionPacket();
        var strings = new List<string>();
        var unknownFields = new List<uint>();
        var hasType = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            if ((tagValue >> 3) > int.MaxValue)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber == StringsField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out var text))
                {
                    return false;
                }

                strings.Add(text);
                continue;
            }

            if (!IsKnownVarintField(fieldNumber))
            {
                if (!reader.TrySkip(wireType))
                {
                    return false;
                }

                unknownFields.Add((uint)fieldNumber);
                continue;
            }

            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ObjIdField:
                    if (!TryToUInt32(value, out var objId))
                    {
                        return false;
                    }

                    result = result with { ObjId = objId };
                    break;
                case TargetUidField:
                    if (!TryToUInt32(value, out var targetUid))
                    {
                        return false;
                    }

                    result = result with { TargetUid = targetUid };
                    break;
                case DwParamField:
                    if (!TryToUInt32(value, out var dwParam))
                    {
                        return false;
                    }

                    result = result with { DwParam = dwParam };
                    break;
                case DwParam2Field:
                    result = result with { DwParam2 = unchecked((int)value) };
                    break;
                case DwParam3Field:
                    result = result with { DwParam3 = unchecked((long)value) };
                    break;
                case SucDoneField:
                    result = result with { SucDone = value != 0 };
                    break;
                case TargetPositionXField:
                    if (!TryToUInt32(value, out var targetPositionX))
                    {
                        return false;
                    }

                    result = result with { TargetPositionX = targetPositionX };
                    break;
                case TargetPositionYField:
                    if (!TryToUInt32(value, out var targetPositionY))
                    {
                        return false;
                    }

                    result = result with { TargetPositionY = targetPositionY };
                    break;
                case TimestampField:
                    if (!TryToUInt32(value, out var timestamp))
                    {
                        return false;
                    }

                    result = result with { Timestamp = timestamp };
                    break;
                case NpcIdField:
                    result = result with { NpcId = unchecked((int)value) };
                    break;
                case CheatLogData1Field:
                    if (!TryToUInt32(value, out var cheatLogData1))
                    {
                        return false;
                    }

                    result = result with { CheatLogData1 = cheatLogData1 };
                    break;
                case TypeField:
                    if (!TryToUInt16(value, out var type))
                    {
                        return false;
                    }

                    result = result with { Type = (MsgActionType)type };
                    hasType = true;
                    break;
                case FacingField:
                    if (!TryToUInt16(value, out var facing))
                    {
                        return false;
                    }

                    result = result with { Facing = facing };
                    break;
                case PositionXField:
                    if (!TryToUInt32(value, out var positionX))
                    {
                        return false;
                    }

                    result = result with { PositionX = positionX };
                    break;
                case PositionYField:
                    if (!TryToUInt32(value, out var positionY))
                    {
                        return false;
                    }

                    result = result with { PositionY = positionY };
                    break;
                case CheatLogData2Field:
                    if (!TryToUInt32(value, out var cheatLogData2))
                    {
                        return false;
                    }

                    result = result with { CheatLogData2 = cheatLogData2 };
                    break;
                case MapIdField:
                    if (!TryToUInt32(value, out var mapId))
                    {
                        return false;
                    }

                    result = result with { MapId = mapId };
                    break;
                case CheatLogData3Field:
                    if (!TryToUInt32(value, out var cheatLogData3))
                    {
                        return false;
                    }

                    result = result with { CheatLogData3 = cheatLogData3 };
                    break;
                case ScheduleTimeField:
                    result = result with { ScheduleTime = unchecked((int)value) };
                    break;
                case LinkIndexField:
                    result = result with { LinkIndex = unchecked((int)value) };
                    break;
                case CheatLogData4Field:
                    if (!TryToUInt32(value, out var cheatLogData4))
                    {
                        return false;
                    }

                    result = result with { CheatLogData4 = cheatLogData4 };
                    break;
                case RemainTimeField:
                    result = result with { RemainTime = unchecked((int)value) };
                    break;
                case MountChargeField:
                    result = result with { MountCharge = value != 0 };
                    break;
                default:
                    return false;
            }
        }

        if (!hasType)
        {
            return false;
        }

        packet = result with
        {
            Strings = strings,
            UnknownFields = unknownFields.Count == 0 ? [] : unknownFields.ToArray(),
        };
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, ObjIdField, ObjId);
        WriteOptionalVarintField(payload, TargetUidField, TargetUid);
        WriteOptionalVarintField(payload, DwParamField, DwParam);
        WriteOptionalSignedField(payload, DwParam2Field, DwParam2);
        WriteOptionalSignedField(payload, DwParam3Field, DwParam3);
        WriteOptionalBoolField(payload, SucDoneField, SucDone);
        WriteOptionalVarintField(payload, TargetPositionXField, TargetPositionX);
        WriteOptionalVarintField(payload, TargetPositionYField, TargetPositionY);
        WriteVarintField(payload, TimestampField, Timestamp);
        WriteOptionalSignedField(payload, NpcIdField, NpcId);
        WriteOptionalVarintField(payload, CheatLogData1Field, CheatLogData1);
        WriteVarintField(payload, TypeField, (ushort)Type);
        WriteVarintField(payload, FacingField, Facing);
        WriteVarintField(payload, PositionXField, PositionX);
        WriteVarintField(payload, PositionYField, PositionY);
        WriteOptionalVarintField(payload, CheatLogData2Field, CheatLogData2);
        WriteOptionalVarintField(payload, MapIdField, MapId);
        WriteOptionalVarintField(payload, CheatLogData3Field, CheatLogData3);
        WriteOptionalSignedField(payload, ScheduleTimeField, ScheduleTime);
        WriteOptionalSignedField(payload, LinkIndexField, LinkIndex);
        WriteOptionalVarintField(payload, CheatLogData4Field, CheatLogData4);
        WriteOptionalSignedField(payload, RemainTimeField, RemainTime);
        WriteOptionalBoolField(payload, MountChargeField, MountCharge);
        foreach (var text in Strings)
        {
            WriteString(payload, StringsField, text);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteOptionalVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, value);
        }
    }

    private static void WriteOptionalSignedField(List<byte> payload, int fieldNumber, long value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, unchecked((ulong)value));
        }
    }

    private static void WriteOptionalBoolField(List<byte> payload, int fieldNumber, bool value)
    {
        if (value)
        {
            WriteVarintField(payload, fieldNumber, 1);
        }
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteString(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)bytes.Length);
        payload.AddRange(bytes);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private static bool TryToUInt32(ulong value, out uint result)
    {
        if (value > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryToUInt16(ulong value, out ushort result)
    {
        if (value > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
        return true;
    }

    private static bool IsKnownVarintField(int fieldNumber)
    {
        return fieldNumber is ObjIdField
            or TargetUidField
            or DwParamField
            or DwParam2Field
            or DwParam3Field
            or SucDoneField
            or TargetPositionXField
            or TargetPositionYField
            or TimestampField
            or NpcIdField
            or CheatLogData1Field
            or TypeField
            or FacingField
            or PositionXField
            or PositionYField
            or CheatLogData2Field
            or MapIdField
            or CheatLogData3Field
            or ScheduleTimeField
            or LinkIndexField
            or CheatLogData4Field
            or RemainTimeField
            or MountChargeField;
    }

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = Encoding.UTF8.GetString(_payload.Slice(_offset, (int)length));
            _offset += (int)length;
            return true;
        }

        public bool TrySkip(int wireType)
        {
            return wireType switch
            {
                VarintWireType => TryReadVarint(out _),
                Fixed64WireType => TryAdvance(sizeof(ulong)),
                LengthDelimitedWireType => TrySkipLengthDelimited(),
                Fixed32WireType => TryAdvance(sizeof(uint)),
                _ => false,
            };
        }

        private bool TrySkipLengthDelimited()
        {
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue)
            {
                return false;
            }

            return TryAdvance((int)length);
        }

        private bool TryAdvance(int count)
        {
            if (count < 0 ||
                _offset > _payload.Length - count)
            {
                return false;
            }

            _offset += count;
            return true;
        }
    }
}
