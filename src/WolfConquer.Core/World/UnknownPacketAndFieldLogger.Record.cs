using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class UnknownPacketAndFieldLogger
{
    private sealed record FieldDescription(int Number, int WireType, int TagOffset, int ValueOffset,
        int EncodedLength, int? DataLength, bool Known);

    private sealed record ScanResult(string Status, FieldDescription[] Fields, bool Unexpected);

    private sealed record DiagnosticRecord(int Version, DateTimeOffset TimeUtc, string Profile, ushort Opcode,
        ushort HeaderLength, long PayloadLength, bool UnknownOpcode, string FrameStatus, string? Schema,
        string ScanStatus, FieldDescription[] Fields, string? RawPayloadHex, bool RawPayloadTruncated);

    // Only the inspected D13 daily-task schema is currently eligible. The separate
    // Chinese7935 2132/2350 codecs do not establish a D13 runtime schema.
    private const string DailyTaskSchema = "VisibleDx8D13.CMsgRandomDailyTask.fields1-8";

    private static bool HasDailyTaskContract(ConquerProtocolProfile profile, ushort opcode) =>
        profile == ConquerProtocolProfile.VisibleDx8D13 && opcode == D13RandomDailyTaskPacket.Opcode;

    private static bool IsKnownDailyTaskField(int number, int wireType) =>
        number is >= 1 and <= 7 && wireType == 0 || number == 8 && wireType is 0 or 2;

    private static bool IsCoherent(PacketFrame frame) =>
        frame.Payload.Length <= ushort.MaxValue - PacketHeader.Size &&
        frame.Header.Length == frame.Payload.Length + PacketHeader.Size &&
        frame.Frame.Length == frame.Header.Length;

    private static bool IsSensitiveOpcode(ushort opcode) => opcode is
        // Account authentication/forwarding and internal login fixtures.
        1000 or 1001 or 1052 or 1059 or 1637 or 1942 or
        // Snapshot/handoff, PIN, login, character naming, chat/broadcast/string,
        // and NPC reply (also used for PIN/email text).
        2000 or 2002 or 2005 or 2036 or 2052 or 2065 or 2150 or 2179 or 2255;
}
