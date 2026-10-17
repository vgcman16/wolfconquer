using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyDragonSkinType : uint
{
    MyShow = 0,
    PlayerView = 1,
    Unlock = 2,
    Retrieve = 3,
    Select = 4,
    Restore = 5,
    Effect = 6,
}

public sealed record LegacyDragonSkinState(uint EquippedSkin, IReadOnlyList<uint> OwnedSkins)
{
    private const string AttributeName = "AnimaSkin";

    public static LegacyDragonSkinState Empty { get; } = new(0, Array.Empty<uint>());

    public static LegacyDragonSkinState FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.Attributes.TryGetValue(AttributeName, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return Empty;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Empty;
        }

        var equippedSkin = TryParseUInt32(parts[0], out var equipped)
            ? equipped
            : 0;
        var count = parts.Length > 1 && int.TryParse(
            parts[1],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedCount)
            ? Math.Max(0, parsedCount)
            : 0;

        var ownedSkins = new List<uint>(count);
        for (var index = 0; index < count && index + 2 < parts.Length; index++)
        {
            if (TryParseUInt32(parts[index + 2], out var skinType) &&
                skinType != 0 &&
                !ownedSkins.Contains(skinType))
            {
                ownedSkins.Add(skinType);
            }
        }

        return ownedSkins.Count == 0 && equippedSkin == 0
            ? Empty
            : new LegacyDragonSkinState(equippedSkin, ownedSkins.ToArray());
    }

    public bool Owns(uint skinType)
    {
        foreach (var ownedSkin in OwnedSkins)
        {
            if (ownedSkin == skinType)
            {
                return true;
            }
        }

        return false;
    }

    public LegacyDragonSkinState WithEquipped(uint skinType)
    {
        return skinType != 0 && Owns(skinType)
            ? this with { EquippedSkin = skinType }
            : this;
    }

    public LegacyDragonSkinState AddOwned(uint skinType)
    {
        if (skinType == 0 || Owns(skinType))
        {
            return this;
        }

        return this with { OwnedSkins = OwnedSkins.Concat([skinType]).ToArray() };
    }

    public LegacyDragonSkinState RemoveOwned(uint skinType)
    {
        if (skinType == 0 || !Owns(skinType))
        {
            return this;
        }

        var ownedSkins = OwnedSkins.Where(ownedSkin => ownedSkin != skinType).ToArray();
        return this with
        {
            EquippedSkin = ownedSkins.Contains(EquippedSkin) ? EquippedSkin : 0,
            OwnedSkins = ownedSkins,
        };
    }

    public LegacyDragonSkinState Restore()
    {
        return this with { EquippedSkin = 0 };
    }

    public string ToSnapshotValue()
    {
        var parts = new List<string>(OwnedSkins.Count + 2)
        {
            EquippedSkin.ToString(CultureInfo.InvariantCulture),
            OwnedSkins.Count.ToString(CultureInfo.InvariantCulture),
        };

        foreach (var skinType in OwnedSkins)
        {
            parts.Add(skinType.ToString(CultureInfo.InvariantCulture));
        }

        return string.Join('/', parts) + "/";
    }

    private static bool TryParseUInt32(string value, out uint parsed)
    {
        return uint.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out parsed);
    }
}

public sealed record LegacyDragonSkinPacket(
    LegacyDragonSkinType Type,
    uint UserID,
    uint SkinType,
    bool OK)
{
    private const int TypeField = 1;
    private const int UserIDField = 2;
    private const int SkinTypeField = 3;
    private const int OKField = 4;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyAnimaSkin;

    public static LegacyDragonSkinPacket CreateStartup(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var userID = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        var equippedSkin = LegacyDragonSkinState.FromSnapshot(snapshot).EquippedSkin;
        return equippedSkin == 0
            ? new LegacyDragonSkinPacket(LegacyDragonSkinType.Restore, userID, SkinType: 0, OK: true)
            : new LegacyDragonSkinPacket(LegacyDragonSkinType.MyShow, userID, equippedSkin, OK: false);
    }

    public static bool TryParse(PacketFrame frame, out LegacyDragonSkinPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = LegacyDragonSkinType.MyShow;
        var userID = 0u;
        var skinType = 0u;
        var ok = false;

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
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out var typeValue) ||
                        !TryToType(typeValue, out type))
                    {
                        return false;
                    }

                    break;
                case UserIDField:
                    if (!TryReadUInt32(ref reader, wireType, out userID))
                    {
                        return false;
                    }

                    break;
                case SkinTypeField:
                    if (!TryReadUInt32(ref reader, wireType, out skinType))
                    {
                        return false;
                    }

                    break;
                case OKField:
                    if (!TryReadBool(ref reader, wireType, out ok))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        packet = new LegacyDragonSkinPacket(type, userID, skinType, ok);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        if (Type != LegacyDragonSkinType.MyShow)
        {
            WriteVarintField(payload, TypeField, (uint)Type);
        }

        if (UserID != 0)
        {
            WriteVarintField(payload, UserIDField, UserID);
        }

        if (SkinType != 0)
        {
            WriteVarintField(payload, SkinTypeField, SkinType);
        }

        if (OK)
        {
            WriteVarintField(payload, OKField, 1);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32(ref ProtoReader reader, int wireType, out uint value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    private static bool TryReadBool(ref ProtoReader reader, int wireType, out bool value)
    {
        value = false;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > 1)
        {
            return false;
        }

        value = raw == 1;
        return true;
    }

    private static bool TryToType(uint value, out LegacyDragonSkinType type)
    {
        type = (LegacyDragonSkinType)value;
        return type is >= LegacyDragonSkinType.MyShow and <= LegacyDragonSkinType.Effect;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt32.");
        }

        return (uint)value;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
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
    }
}
