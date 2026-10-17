using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyNpcServerAction : ushort
{
    Script = 0,
    Statue = 3,
    Cursor = 5,
    PlaceFurniture = 6,
}

public enum LegacyNpcScriptRequestRejectionReason
{
    None = 0,
    ClosedOption,
    MissingNpcId,
    InvalidInteractType,
    UnsupportedInteractType,
}

public readonly record struct LegacyNpcServerRequestPacket(
    LegacyNpcServerAction Action,
    uint NpcId,
    byte OptionId,
    byte InteractType,
    string Input)
{
    private const int ActionField = 1;
    private const int NpcIdField = 2;
    private const int OptionIdField = 3;
    private const int InteractTypeField = 4;
    private const int InputField = 5;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNpcServer;

    public static LegacyNpcServerRequestPacket Script(uint npcId, byte optionId = 0, byte interactType = 1, string input = "")
    {
        ArgumentNullException.ThrowIfNull(input);
        return new LegacyNpcServerRequestPacket(LegacyNpcServerAction.Script, npcId, optionId, interactType, input);
    }

    public static LegacyNpcServerRequestPacket Script(
        uint npcId,
        byte optionId,
        LegacyNpcReplyInteractType interactType,
        string input = "")
    {
        return Script(npcId, optionId, (byte)interactType, input);
    }

    public static bool TryCreateScriptRequest(
        LegacyNpcDialogRequestPacket reply,
        uint activeNpcId,
        out LegacyNpcServerRequestPacket request)
    {
        return TryCreateScriptRequest(reply, activeNpcId, out request, out _);
    }

    public static bool TryCreateScriptRequest(
        LegacyNpcDialogRequestPacket reply,
        uint activeNpcId,
        out LegacyNpcServerRequestPacket request,
        out LegacyNpcScriptRequestRejectionReason rejectionReason)
    {
        request = default;
        rejectionReason = TryResolveScriptRequest(reply, activeNpcId, out var npcId, out var interactType);

        if (rejectionReason != LegacyNpcScriptRequestRejectionReason.None)
        {
            return false;
        }

        request = Script(npcId, reply.OptionId, interactType, reply.Input ?? string.Empty);
        return true;
    }

    public bool IsClosedOption => OptionId == LegacyNpcReplyPacket.ClosedOptionId;

    public bool TryGetInteractType(out LegacyNpcReplyInteractType interactType)
    {
        interactType = default;
        if (!Enum.IsDefined(typeof(LegacyNpcReplyInteractType), InteractType))
        {
            return false;
        }

        interactType = (LegacyNpcReplyInteractType)InteractType;
        return true;
    }

    public string FormatScriptKey()
    {
        return FormatScriptKey(NpcId, OptionId, InteractType);
    }

    public static string FormatScriptKey(uint npcId, byte optionId, byte interactType)
    {
        return string.Concat(
            "npcId=",
            npcId.ToString(CultureInfo.InvariantCulture),
            " optionId=",
            optionId.ToString(CultureInfo.InvariantCulture),
            " interactType=",
            FormatInteractTypeForLog(interactType));
    }

    public string DescribeForLog()
    {
        return string.Concat(
            "action=",
            Action.ToString(),
            " ",
            FormatScriptKey(),
            " inputLength=",
            (Input?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
    }

    public static string FormatInteractTypeForLog(byte interactType)
    {
        if (Enum.IsDefined(typeof(LegacyNpcReplyInteractType), interactType))
        {
            return ((LegacyNpcReplyInteractType)interactType).ToString();
        }

        return string.Concat("Invalid(", interactType.ToString(CultureInfo.InvariantCulture), ")");
    }

    public static bool TryParse(PacketFrame frame, out LegacyNpcServerRequestPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        LegacyNpcServerAction action = LegacyNpcServerAction.Script;
        uint npcId = 0;
        byte optionId = 0;
        byte interactType = 0;
        var input = string.Empty;
        var hasAction = false;
        var hasNpcId = false;
        var hasOptionId = false;
        var hasInteractType = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ActionField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out var rawAction) ||
                        rawAction > ushort.MaxValue)
                    {
                        return false;
                    }

                    action = (LegacyNpcServerAction)rawAction;
                    hasAction = true;
                    break;
                case NpcIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out npcId))
                    {
                        return false;
                    }

                    hasNpcId = true;
                    break;
                case OptionIdField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out var rawOptionId) ||
                        rawOptionId > byte.MaxValue)
                    {
                        return false;
                    }

                    optionId = (byte)rawOptionId;
                    hasOptionId = true;
                    break;
                case InteractTypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out var rawInteractType) ||
                        rawInteractType > byte.MaxValue)
                    {
                        return false;
                    }

                    interactType = (byte)rawInteractType;
                    hasInteractType = true;
                    break;
                case InputField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadString(out input))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasAction ||
            !hasNpcId ||
            !hasOptionId ||
            !hasInteractType)
        {
            return false;
        }

        packet = new LegacyNpcServerRequestPacket(action, npcId, optionId, interactType, input);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, ActionField, (ushort)Action);
        LegacyProtoWire.WriteVarintField(payload, NpcIdField, NpcId);
        LegacyProtoWire.WriteVarintField(payload, OptionIdField, OptionId);
        LegacyProtoWire.WriteVarintField(payload, InteractTypeField, InteractType);
        if (Input.Length > 0)
        {
            LegacyProtoWire.WriteStringField(payload, InputField, Input);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static LegacyNpcScriptRequestRejectionReason TryResolveScriptRequest(
        LegacyNpcDialogRequestPacket reply,
        uint activeNpcId,
        out uint npcId,
        out LegacyNpcReplyInteractType interactType)
    {
        npcId = reply.ResolveNpcId(activeNpcId);
        interactType = default;

        if (reply.IsClosedOption)
        {
            return LegacyNpcScriptRequestRejectionReason.ClosedOption;
        }

        if (!reply.TryGetInteractType(out interactType))
        {
            return LegacyNpcScriptRequestRejectionReason.InvalidInteractType;
        }

        if (!IsScriptReplyInteractType(interactType))
        {
            return LegacyNpcScriptRequestRejectionReason.UnsupportedInteractType;
        }

        if (npcId == 0)
        {
            return LegacyNpcScriptRequestRejectionReason.MissingNpcId;
        }

        return LegacyNpcScriptRequestRejectionReason.None;
    }

    private static bool IsScriptReplyInteractType(LegacyNpcReplyInteractType interactType)
    {
        return interactType is LegacyNpcReplyInteractType.Dialog or
            LegacyNpcReplyInteractType.Option or
            LegacyNpcReplyInteractType.Input;
    }
}
