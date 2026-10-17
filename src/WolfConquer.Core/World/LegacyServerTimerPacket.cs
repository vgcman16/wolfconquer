using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyServerTimerPacket(
    int Year,
    int Month,
    int DayOfYear,
    int DayOfMonth,
    int Hour,
    int Minute,
    int Second)
{
    private const int TypeField = 1;
    private const int YearField = 2;
    private const int MonthField = 3;
    private const int DayOfYearField = 4;
    private const int DayOfMonthField = 5;
    private const int HourField = 6;
    private const int MinuteField = 7;
    private const int SecondField = 8;
    private const uint TypeValue = 0;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyServerTimer;

    public static LegacyServerTimerPacket FromDateTime(DateTime time)
    {
        return new LegacyServerTimerPacket(
            time.Year - 1900,
            time.Month - 1,
            time.DayOfYear,
            time.Day,
            time.Hour,
            time.Minute,
            time.Second);
    }

    public static bool TryParse(PacketFrame frame, out LegacyServerTimerPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new TimerProtoReader(frame.Payload.ToArray());
        var fieldsSeen = 0;
        int year = 0;
        int month = 0;
        int dayOfYear = 0;
        int dayOfMonth = 0;
        int hour = 0;
        int minute = 0;
        int second = 0;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType || !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (value != TypeValue)
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << TypeField;
                    break;
                case YearField:
                    if (!TryReadInt32(value, out year))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << YearField;
                    break;
                case MonthField:
                    if (!TryReadInt32(value, out month))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << MonthField;
                    break;
                case DayOfYearField:
                    if (!TryReadInt32(value, out dayOfYear))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << DayOfYearField;
                    break;
                case DayOfMonthField:
                    if (!TryReadInt32(value, out dayOfMonth))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << DayOfMonthField;
                    break;
                case HourField:
                    if (!TryReadInt32(value, out hour))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << HourField;
                    break;
                case MinuteField:
                    if (!TryReadInt32(value, out minute))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << MinuteField;
                    break;
                case SecondField:
                    if (!TryReadInt32(value, out second))
                    {
                        return false;
                    }

                    fieldsSeen |= 1 << SecondField;
                    break;
                default:
                    return false;
            }
        }

        const int requiredFields =
            (1 << TypeField) |
            (1 << YearField) |
            (1 << MonthField) |
            (1 << DayOfYearField) |
            (1 << DayOfMonthField) |
            (1 << HourField) |
            (1 << MinuteField) |
            (1 << SecondField);

        if ((fieldsSeen & requiredFields) != requiredFields)
        {
            return false;
        }

        packet = new LegacyServerTimerPacket(year, month, dayOfYear, dayOfMonth, hour, minute, second);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        Span<byte> payload = stackalloc byte[32];
        var length = WritePayload(payload);
        var writer = new PacketWriter(Opcode, length);
        payload[..length].CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private int WritePayload(Span<byte> payload)
    {
        var offset = 0;
        WriteField(payload, ref offset, TypeField, TypeValue);
        WriteField(payload, ref offset, YearField, CheckedUInt32(Year));
        WriteField(payload, ref offset, MonthField, CheckedUInt32(Month));
        WriteField(payload, ref offset, DayOfYearField, CheckedUInt32(DayOfYear));
        WriteField(payload, ref offset, DayOfMonthField, CheckedUInt32(DayOfMonth));
        WriteField(payload, ref offset, HourField, CheckedUInt32(Hour));
        WriteField(payload, ref offset, MinuteField, CheckedUInt32(Minute));
        WriteField(payload, ref offset, SecondField, CheckedUInt32(Second));
        return offset;
    }

    private static void WriteField(Span<byte> payload, ref int offset, int fieldNumber, uint value)
    {
        WriteVarint(payload, ref offset, (uint)(fieldNumber << 3));
        WriteVarint(payload, ref offset, value);
    }

    private static void WriteVarint(Span<byte> payload, ref int offset, uint value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload[offset++] = next;
        }
        while (value != 0);
    }

    private static bool TryReadInt32(ulong value, out int result)
    {
        if (value > int.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    private static uint CheckedUInt32(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Legacy server timer values cannot be negative.");
        }

        return (uint)value;
    }

    private ref struct TimerProtoReader(ReadOnlySpan<byte> payload)
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
