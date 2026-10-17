using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyHairFaceStorageType : byte
{
    Hairstyle = 0,
    Avatar = 1,
}

public enum LegacyHairFaceStorageAction : byte
{
    Login = 0,
    Add = 1,
    Equip = 3,
    Buy = 4,
    AddColor = 5,
    EquipColor = 6,
    BuyColor = 7,
    Avatars = 8,
    HairStyles = 9,
}

public readonly record struct LegacyHairFaceStorageOperation(uint Id, uint DwParam);

public sealed record LegacyHairFaceStorageItem(
    LegacyHairFaceStorageType Type,
    uint Id,
    bool Equipped,
    uint Unknown4,
    IReadOnlyList<long> Colors)
{
    public static LegacyHairFaceStorageItem DefaultTabItem()
    {
        return new LegacyHairFaceStorageItem(
            LegacyHairFaceStorageType.Hairstyle,
            Id: 0,
            Equipped: false,
            Unknown4: 0,
            [0]);
    }
}

public sealed record LegacyHairFaceStoragePacket(
    LegacyHairFaceStorageAction Action,
    LegacyHairFaceStorageOperation? Hair,
    LegacyHairFaceStorageOperation? HairColor,
    IReadOnlyList<LegacyHairFaceStorageItem> AvatarsOrHairTap,
    IReadOnlyList<LegacyHairFaceStorageItem> Items)
{
    private const int ActionField = 1;
    private const int HairField = 2;
    private const int HairColorField = 3;
    private const int AvatarsOrHairTapField = 4;
    private const int ItemsField = 5;
    private const int OperationIdField = 1;
    private const int OperationDwParamField = 2;
    private const int ItemTypeField = 1;
    private const int ItemIdField = 2;
    private const int ItemEquippedField = 3;
    private const int ItemUnknown4Field = 4;
    private const int ItemColorsField = 5;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyHairFaceStorage;

    public static LegacyHairFaceStoragePacket EmptyLoginResponse()
    {
        return new LegacyHairFaceStoragePacket(
            LegacyHairFaceStorageAction.Login,
            Hair: null,
            HairColor: null,
            AvatarsOrHairTap: [],
            Items: []);
    }

    public static LegacyHairFaceStoragePacket TabResponse(LegacyHairFaceStorageAction action)
    {
        if (action is not LegacyHairFaceStorageAction.Avatars and not LegacyHairFaceStorageAction.HairStyles)
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Only Avatars and HairStyles create tab responses.");
        }

        return new LegacyHairFaceStoragePacket(
            action,
            Hair: null,
            HairColor: null,
            AvatarsOrHairTap: [LegacyHairFaceStorageItem.DefaultTabItem()],
            Items: []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyHairFaceStoragePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var action = LegacyHairFaceStorageAction.Login;
        LegacyHairFaceStorageOperation? hair = null;
        LegacyHairFaceStorageOperation? hairColor = null;
        var avatarsOrHairTap = new List<LegacyHairFaceStorageItem>();
        var items = new List<LegacyHairFaceStorageItem>();
        var hasAction = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case ActionField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadVarint(out var actionValue) ||
                        actionValue > byte.MaxValue)
                    {
                        return false;
                    }

                    action = (LegacyHairFaceStorageAction)(byte)actionValue;
                    hasAction = true;
                    break;
                case HairField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var hairPayload) ||
                        !TryParseOperation(hairPayload, out var parsedHair))
                    {
                        return false;
                    }

                    hair = parsedHair;
                    break;
                case HairColorField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var hairColorPayload) ||
                        !TryParseOperation(hairColorPayload, out var parsedHairColor))
                    {
                        return false;
                    }

                    hairColor = parsedHairColor;
                    break;
                case AvatarsOrHairTapField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var tabPayload) ||
                        !TryParseItem(tabPayload, out var tabItem))
                    {
                        return false;
                    }

                    avatarsOrHairTap.Add(tabItem);
                    break;
                case ItemsField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var itemPayload) ||
                        !TryParseItem(itemPayload, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;
                default:
                    return false;
            }
        }

        if (!hasAction)
        {
            return false;
        }

        packet = new LegacyHairFaceStoragePacket(action, hair, hairColor, avatarsOrHairTap, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(AvatarsOrHairTap);
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        WriteVarintField(payload, ActionField, (uint)Action);

        if (Hair is { } hair)
        {
            WriteBytes(payload, HairField, CreateOperationPayload(hair));
        }

        if (HairColor is { } hairColor)
        {
            WriteBytes(payload, HairColorField, CreateOperationPayload(hairColor));
        }

        foreach (var item in AvatarsOrHairTap)
        {
            WriteBytes(payload, AvatarsOrHairTapField, CreateItemPayload(item));
        }

        foreach (var item in Items)
        {
            WriteBytes(payload, ItemsField, CreateItemPayload(item));
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static List<byte> CreateOperationPayload(LegacyHairFaceStorageOperation operation)
    {
        var payload = new List<byte>();
        WriteVarintField(payload, OperationIdField, operation.Id);
        WriteVarintField(payload, OperationDwParamField, operation.DwParam);
        return payload;
    }

    private static List<byte> CreateItemPayload(LegacyHairFaceStorageItem item)
    {
        ArgumentNullException.ThrowIfNull(item.Colors);

        var payload = new List<byte>();
        WriteVarintField(payload, ItemTypeField, (uint)item.Type);
        WriteVarintField(payload, ItemIdField, item.Id);
        WriteVarintField(payload, ItemEquippedField, item.Equipped ? 1u : 0u);
        WriteVarintField(payload, ItemUnknown4Field, item.Unknown4);
        foreach (var color in item.Colors)
        {
            if (color < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(item), color, "Legacy hair-face colors must be non-negative.");
            }

            WriteVarintField(payload, ItemColorsField, (ulong)color);
        }

        return payload;
    }

    private static bool TryParseOperation(byte[] payload, out LegacyHairFaceStorageOperation operation)
    {
        operation = default;
        var reader = new ProtoReader(payload);
        var id = 0u;
        var dwParam = 0u;
        var hasId = false;
        var hasDwParam = false;

        while (!reader.End)
        {
            if (!TryReadNestedUInt32(ref reader, out var fieldNumber, out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case OperationIdField:
                    id = value;
                    hasId = true;
                    break;
                case OperationDwParamField:
                    dwParam = value;
                    hasDwParam = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasId || !hasDwParam)
        {
            return false;
        }

        operation = new LegacyHairFaceStorageOperation(id, dwParam);
        return true;
    }

    private static bool TryParseItem(byte[] payload, out LegacyHairFaceStorageItem item)
    {
        item = default!;
        var reader = new ProtoReader(payload);
        var type = LegacyHairFaceStorageType.Hairstyle;
        var id = 0u;
        var equipped = false;
        var unknown4 = 0u;
        var colors = new List<long>();
        var hasType = false;
        var hasId = false;
        var hasEquipped = false;
        var hasUnknown4 = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType || !reader.TryReadVarint(out var rawValue))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemTypeField:
                    if (rawValue > byte.MaxValue)
                    {
                        return false;
                    }

                    type = (LegacyHairFaceStorageType)(byte)rawValue;
                    hasType = true;
                    break;
                case ItemIdField:
                    if (rawValue > uint.MaxValue)
                    {
                        return false;
                    }

                    id = (uint)rawValue;
                    hasId = true;
                    break;
                case ItemEquippedField:
                    if (rawValue > 1)
                    {
                        return false;
                    }

                    equipped = rawValue != 0;
                    hasEquipped = true;
                    break;
                case ItemUnknown4Field:
                    if (rawValue > uint.MaxValue)
                    {
                        return false;
                    }

                    unknown4 = (uint)rawValue;
                    hasUnknown4 = true;
                    break;
                case ItemColorsField:
                    if (rawValue > long.MaxValue)
                    {
                        return false;
                    }

                    colors.Add((long)rawValue);
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasId || !hasEquipped || !hasUnknown4)
        {
            return false;
        }

        item = new LegacyHairFaceStorageItem(type, id, equipped, unknown4, colors);
        return true;
    }

    private static bool TryReadNestedUInt32(
        ref ProtoReader reader,
        out int fieldNumber,
        out uint value)
    {
        fieldNumber = 0;
        value = 0;

        if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
        {
            return false;
        }

        fieldNumber = (int)(tagValue >> 3);
        var wireType = (int)(tagValue & 0x7);
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var rawValue) ||
            rawValue > uint.MaxValue)
        {
            return false;
        }

        value = (uint)rawValue;
        return true;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarintField(payload, fieldNumber, (ulong)value);
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
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

        public bool TryReadBytes(out byte[] value)
        {
            value = [];

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length).ToArray();
            _offset += (int)length;
            return true;
        }
    }
}
