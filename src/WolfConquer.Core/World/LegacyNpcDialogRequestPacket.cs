using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyNpcDialogRequestPacket(
    uint NpcId,
    ushort Mesh,
    byte OptionId,
    byte InteractType,
    byte Action,
    string Input)
{
    private const int NpcIdOffset = 0;
    private const int MeshOffset = NpcIdOffset + sizeof(uint);
    private const int OptionIdOffset = MeshOffset + sizeof(ushort);
    private const int InteractTypeOffset = OptionIdOffset + sizeof(byte);
    private const int ActionOffset = InteractTypeOffset + sizeof(byte);
    private const int InputLengthOffset = ActionOffset + sizeof(byte);
    private const int InputOffset = InputLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyNpcReply;
    public const byte MessageBoxInteractType = 6;
    public const byte ClosedOptionId = LegacyNpcReplyPacket.ClosedOptionId;

    public bool IsMessageBox => InteractType == MessageBoxInteractType;

    public bool IsAcceptedMessageBox => IsMessageBox && OptionId == 0;

    public bool IsClosedOption => OptionId == ClosedOptionId;

    public bool IsScriptReply =>
        !IsClosedOption &&
        TryGetInteractType(out var interactType) &&
        interactType is LegacyNpcReplyInteractType.Dialog or
            LegacyNpcReplyInteractType.Option or
            LegacyNpcReplyInteractType.Input;

    public uint ResolveNpcId(uint activeNpcId)
    {
        return NpcId != 0 ? NpcId : activeNpcId;
    }

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

    public bool TryCreateScriptRequest(uint activeNpcId, out LegacyNpcServerRequestPacket request)
    {
        return TryCreateScriptRequest(activeNpcId, out request, out _);
    }

    public bool TryCreateScriptRequest(
        uint activeNpcId,
        out LegacyNpcServerRequestPacket request,
        out LegacyNpcScriptRequestRejectionReason rejectionReason)
    {
        return LegacyNpcServerRequestPacket.TryCreateScriptRequest(
            this,
            activeNpcId,
            out request,
            out rejectionReason);
    }

    public string FormatScriptKey(uint activeNpcId = 0)
    {
        return LegacyNpcServerRequestPacket.FormatScriptKey(ResolveNpcId(activeNpcId), OptionId, InteractType);
    }

    public string DescribeForLog(uint activeNpcId = 0)
    {
        return string.Concat(
            "packetNpcId=",
            NpcId.ToString(CultureInfo.InvariantCulture),
            " activeNpcId=",
            activeNpcId.ToString(CultureInfo.InvariantCulture),
            " ",
            FormatScriptKey(activeNpcId),
            " action=",
            Action.ToString(CultureInfo.InvariantCulture),
            " inputLength=",
            (Input?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryParse(PacketFrame frame, out LegacyNpcDialogRequestPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < InputOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var inputLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(InputLengthOffset, sizeof(ushort)));
        var expectedLength = InputOffset + inputLength;
        if (payload.Length < expectedLength ||
            !IsTrailingZeroPadding(payload.AsSpan(expectedLength)))
        {
            return false;
        }

        packet = new LegacyNpcDialogRequestPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(NpcIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(MeshOffset, sizeof(ushort))),
            payload[OptionIdOffset],
            payload[InteractTypeOffset],
            payload[ActionOffset],
            Encoding.UTF8.GetString(payload.AsSpan(InputOffset, inputLength)));
        return true;
    }

    private static bool IsTrailingZeroPadding(ReadOnlySpan<byte> trailing)
    {
        foreach (var value in trailing)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Input);

        var inputBytes = Encoding.UTF8.GetBytes(Input);
        if (inputBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Input), "Legacy NPC dialog input cannot exceed 65,535 bytes.");
        }

        var writer = new PacketWriter(Opcode, InputOffset + inputBytes.Length);
        writer.WriteUInt32(NpcIdOffset, NpcId);
        writer.WriteUInt16(MeshOffset, Mesh);
        writer.Payload[OptionIdOffset] = OptionId;
        writer.Payload[InteractTypeOffset] = InteractType;
        writer.Payload[ActionOffset] = Action;
        writer.WriteUInt16(InputLengthOffset, (ushort)inputBytes.Length);
        inputBytes.CopyTo(writer.Payload[InputOffset..]);
        return writer.ToArray();
    }
}
