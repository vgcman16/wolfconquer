using System.Buffers;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record D13MailLiteralField(ulong Tag, ulong Scalar, byte[] Bytes)
    {
        internal int Number => checked((int)(Tag >> 3));
        internal int Wire => (int)(Tag & 7);
    }

    private static D13MailLiteralField[][] D13MailLiteralRows(byte[] bytes, ulong expectedType, ulong expectedCount)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyMailList, frame.Header.Opcode);
        var fields = D13MailLiteralFields(frame.Payload.ToArray());
        Assert.Equal(expectedType, D13MailLiteralScalar(fields, 1));
        Assert.Equal(expectedCount, D13MailLiteralScalar(fields, 2));
        Assert.Equal(new[] { 1, 2 }, fields.Where(field => field.Number != 3).Select(field => field.Number));
        return fields.Where(field => field.Number == 3).Select(field =>
        {
            Assert.Equal(0x1aUL, field.Tag); // Literal repeated nested mail field, wire2.
            return D13MailLiteralFields(field.Bytes);
        }).ToArray();
    }

    private static void AssertD13MailLiteralText(D13MailLiteralField[] fields, string title, string sender)
    {
        // Inspect the actual bytes independently of LegacyMailListPacket.TryParse
        // and all proposed profile overloads. Current APIs compile this RED input.
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 9, 10 }, fields.Select(field => field.Number));
        foreach (var field in fields.Where(field => field.Number <= 7)) Assert.Equal(0, field.Wire);
        var titleField = Assert.Single(fields, field => field.Number == 9);
        var senderField = Assert.Single(fields, field => field.Number == 10);
        Assert.Equal(0x4aUL, titleField.Tag); // (9 << 3) | 2
        Assert.Equal(0x52UL, senderField.Tag); // (10 << 3) | 2
        Assert.Equal(Encoding.UTF8.GetBytes(title), titleField.Bytes);
        Assert.Equal(Encoding.UTF8.GetBytes(sender), senderField.Bytes);
        Assert.DoesNotContain(fields, field => field.Number == 8);
    }

    private static ulong D13MailLiteralScalar(D13MailLiteralField[] fields, int number)
    {
        var field = Assert.Single(fields, item => item.Number == number);
        Assert.Equal(0, field.Wire);
        return field.Scalar;
    }

    private static D13MailLiteralField[] D13MailLiteralFields(byte[] bytes)
    {
        var fields = new List<D13MailLiteralField>();
        var cursor = 0;
        while (cursor < bytes.Length)
        {
            var tag = D13MailLiteralVarint(bytes, ref cursor);
            Assert.True((tag >> 3) != 0, "Field zero is not a protobuf field.");
            var wire = tag & 7;
            Assert.True(wire is 0 or 2, "Only scalar and string/message wire forms belong in this managed mail frame.");
            if (wire == 0)
            {
                fields.Add(new(tag, D13MailLiteralVarint(bytes, ref cursor), []));
                continue;
            }
            var length = D13MailLiteralVarint(bytes, ref cursor);
            Assert.InRange(length, 0UL, (ulong)(bytes.Length - cursor));
            var count = checked((int)length);
            fields.Add(new(tag, 0, bytes.AsSpan(cursor, count).ToArray()));
            cursor += count;
        }
        return fields.ToArray();
    }

    private static ulong D13MailLiteralVarint(byte[] bytes, ref int cursor)
    {
        ulong result = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            Assert.True(cursor < bytes.Length, "Truncated literal protobuf value.");
            var part = bytes[cursor++];
            if (shift == 63) Assert.InRange(part, (byte)0, (byte)1);
            result |= (ulong)(part & 0x7f) << shift;
            if ((part & 0x80) == 0) return result;
        }
        throw new InvalidDataException("Literal protobuf value exceeds UInt64.");
    }
}
