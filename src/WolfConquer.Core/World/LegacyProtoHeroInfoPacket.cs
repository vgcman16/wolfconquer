using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed class LegacyProtoHeroInfoPacket
{
    private const int FirstNumericField = 1;
    private const int LastNumericField = 49;
    private const int NameField = 46;
    private const int SpouseField = 47;
    private const int BayoField = 50;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    private readonly Dictionary<int, ulong> _numericFields;
    private readonly Dictionary<int, string> _stringFields;

    private LegacyProtoHeroInfoPacket(
        Dictionary<int, ulong> numericFields,
        Dictionary<int, string> stringFields)
    {
        _numericFields = numericFields;
        _stringFields = stringFields;
    }

    public const ushort Opcode = WorldPacketOpcodes.LegacyProtoHeroInfo;

    public static LegacyProtoHeroInfoPacket FromSnapshot(
        CharacterSnapshot snapshot,
        ushort serverId = 2,
        byte initTransfer = 0,
        string? usersRoot = null,
        LegacyNativePlayerRuntime? nativePlayer = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var numeric = CreateRequiredNumericFields();
        var activeTitles = LegacyTitleStoragePacket.ResolveActiveSpecialTitleState(snapshot, usersRoot);
        numeric[1] = CheckedUInt64(snapshot.Uid, nameof(snapshot.Uid));
        numeric[2] = ReadAttributeUInt64(snapshot.Attributes, "AparenceType");
        // Native MsgBuilder.ProtoHeroInfo uses physical Player.Mesh. Only the
        // explicit successful existing-login route supplies this loaded owner.
        numeric[3] = nativePlayer is null ? BuildMesh(snapshot) : nativePlayer.Mesh;
        numeric[4] = CheckedUInt64(snapshot.Hair, nameof(snapshot.Hair));
        numeric[5] = unchecked((ulong)snapshot.Money);
        numeric[6] = CheckedUInt64(snapshot.ConquerPoints, nameof(snapshot.ConquerPoints));
        numeric[7] = CheckedUInt64(snapshot.Experience, nameof(snapshot.Experience));
        numeric[8] = serverId;
        numeric[9] = ReadAttributeUInt64(snapshot.Attributes, "SetLocationType");
        numeric[10] = activeTitles.SpecialTitleId;
        numeric[11] = activeTitles.SpecialWingId;
        numeric[12] = ReadAttributeUInt64(snapshot.Attributes, "VirtutePoints");
        numeric[13] = CheckedUInt64(snapshot.Strength, nameof(snapshot.Strength));
        numeric[14] = CheckedUInt64(snapshot.Agility, nameof(snapshot.Agility));
        numeric[15] = CheckedUInt64(snapshot.Vitality, nameof(snapshot.Vitality));
        numeric[16] = CheckedUInt64(snapshot.Spirit, nameof(snapshot.Spirit));
        numeric[17] = CheckedUInt64(snapshot.AttributePoints, nameof(snapshot.AttributePoints));
        numeric[18] = CheckedUInt64(snapshot.MinHitPoints, nameof(snapshot.MinHitPoints));
        numeric[19] = CheckedUInt64(snapshot.MinMana, nameof(snapshot.MinMana));
        numeric[20] = CheckedUInt64(snapshot.PkPoints, nameof(snapshot.PkPoints));
        numeric[21] = CheckedUInt64(snapshot.Level, nameof(snapshot.Level));
        numeric[22] = CheckedUInt64(snapshot.Class, nameof(snapshot.Class));
        numeric[23] = CheckedUInt64(snapshot.FirstClass, nameof(snapshot.FirstClass));
        numeric[24] = CheckedUInt64(snapshot.SecondClass, nameof(snapshot.SecondClass));
        numeric[25] = ReadNobilityRank(snapshot);
        numeric[26] = CheckedUInt64(snapshot.Reborn, nameof(snapshot.Reborn));
        numeric[27] = initTransfer;
        numeric[28] = ReadAttributeSignedUInt64(snapshot.Attributes, "QuizPoints");
        numeric[29] = ReadAttributeSignedUInt64(snapshot.Attributes, "MainFlag");
        numeric[30] = ReadAttributeSignedUInt64(snapshot.Attributes, "Enilghten");
        numeric[31] = ReadAttributeSignedUInt64(snapshot.Attributes, "EnlightenReceive") * 100;
        numeric[34] = ReadAttributeSignedUInt64(snapshot.Attributes, "VipLevel");
        numeric[35] = ReadAttributeSignedUInt64(snapshot.Attributes, "MyTitle");
        numeric[36] = unchecked((ulong)snapshot.BoundConquerPoints);
        numeric[37] = ReadAttributeSignedUInt64(snapshot.Attributes, "ActiveSublass");
        numeric[38] = ReadAttributeSignedUInt64(snapshot.Attributes, "SubClassHashPoint");
        numeric[39] = ReadAttributeSignedUInt64(snapshot.Attributes, "RacePoints");
        numeric[40] = ReadAttributeSignedUInt64(snapshot.Attributes, "CountryID");
        numeric[42] = ReadAttributeSignedUInt64(snapshot.Attributes, "DominoCode");
        numeric[43] = nativePlayer is null
            ? ReadAttributeSignedUInt64(snapshot.Attributes, "ClassExperience")
            : nativePlayer.ClassExperience;

        var strings = new Dictionary<int, string>
        {
            [NameField] = nativePlayer is null ? snapshot.Name : nativePlayer.Name,
            [SpouseField] = nativePlayer is null
                ? ReadAttributeString(snapshot.Attributes, "Spouse", "None")
                : nativePlayer.Spouse,
            [BayoField] = ReadAttributeString(snapshot.Attributes, "Signature", string.Empty)
        };

        return new LegacyProtoHeroInfoPacket(numeric, strings);
    }

    public ulong GetNumericField(int fieldNumber)
    {
        return _numericFields[fieldNumber];
    }

    public string GetStringField(int fieldNumber)
    {
        return _stringFields[fieldNumber];
    }

    public static bool TryParse(PacketFrame frame, out LegacyProtoHeroInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var numeric = new Dictionary<int, ulong>();
        var strings = new Dictionary<int, string>();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);

            if (fieldNumber is NameField or SpouseField or BayoField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out var value))
                {
                    return false;
                }

                strings[fieldNumber] = value;
                continue;
            }

            if (fieldNumber < FirstNumericField ||
                fieldNumber > LastNumericField ||
                wireType != VarintWireType ||
                !reader.TryReadVarint(out var numericValue))
            {
                return false;
            }

            numeric[fieldNumber] = numericValue;
        }

        if (!HasRequiredFields(numeric, strings))
        {
            return false;
        }

        packet = new LegacyProtoHeroInfoPacket(numeric, strings);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();

        for (var fieldNumber = 1; fieldNumber <= 50; fieldNumber++)
        {
            if (_stringFields.TryGetValue(fieldNumber, out var stringValue))
            {
                WriteString(payload, fieldNumber, stringValue);
                continue;
            }

            WriteVarintField(payload, fieldNumber, _numericFields[fieldNumber]);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static Dictionary<int, ulong> CreateRequiredNumericFields()
    {
        var fields = new Dictionary<int, ulong>();
        for (var fieldNumber = 1; fieldNumber <= 49; fieldNumber++)
        {
            if (fieldNumber is NameField or SpouseField)
            {
                continue;
            }

            fields[fieldNumber] = 0;
        }

        return fields;
    }

    private static bool HasRequiredFields(
        IReadOnlyDictionary<int, ulong> numeric,
        IReadOnlyDictionary<int, string> strings)
    {
        for (var fieldNumber = 1; fieldNumber <= 49; fieldNumber++)
        {
            if (fieldNumber is NameField or SpouseField)
            {
                if (!strings.ContainsKey(fieldNumber))
                {
                    return false;
                }

                continue;
            }

            if (!numeric.ContainsKey(fieldNumber))
            {
                return false;
            }
        }

        return strings.ContainsKey(BayoField);
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

    private static ulong BuildMesh(CharacterSnapshot snapshot)
    {
        var transformationId = ReadAttributeInt64(snapshot.Attributes, "TransformationID");
        return CheckedUInt64(
            checked((transformationId * 10_000_000) + (snapshot.Face * 10_000) + snapshot.Body),
            "Mesh");
    }

    private static ulong ReadNobilityRank(CharacterSnapshot snapshot)
    {
        if (snapshot.PaidNobility.TryGetValue("PaidRank", out var numeric) &&
            ulong.TryParse(numeric, out var paidRank))
        {
            return paidRank;
        }

        if (!snapshot.PaidNobility.TryGetValue("Rank", out var rank))
        {
            return 0;
        }

        return rank.Trim() switch
        {
            "Serf" => 0,
            "Knight" => 1,
            "Baron" => 3,
            "Earl" => 5,
            "Duke" => 7,
            "Prince" => 9,
            "King" => 12,
            _ => 0,
        };
    }

    private static ulong ReadAttributeUInt64(IReadOnlyDictionary<string, string> attributes, string key)
    {
        var value = ReadAttributeInt64(attributes, key);
        return CheckedUInt64(value, key);
    }

    private static ulong ReadAttributeSignedUInt64(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return unchecked((ulong)ReadAttributeInt64(attributes, key));
    }

    private static long ReadAttributeInt64(IReadOnlyDictionary<string, string> attributes, string key)
    {
        if (!attributes.TryGetValue(key, out var value) ||
            !long.TryParse(value, out var parsed))
        {
            return 0;
        }

        return parsed;
    }

    private static string ReadAttributeString(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        string defaultValue)
    {
        return attributes.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static ulong CheckedUInt64(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy hero field cannot be negative.");
        }

        return (ulong)value;
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
    }
}
