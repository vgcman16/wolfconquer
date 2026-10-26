namespace WolfConquer.Core.Client;

internal sealed partial class ClientConfigGzipInflate
{
    private enum InflateMode { Header, Block, Codes, Stored, Literal, Match, Trailer, Finished }
    private readonly ClientConfigGzipBitInput input;
    private readonly byte[] history = new byte[32768];
    private int historyOffset, historyCount, pendingLength, pendingDistance, pendingLiteral;
    private uint checksum = ClientConfigGzipChecksum.Initial, memberSize;
    private bool finalBlock;
    private InflateMode mode;
    private ClientConfigDeflateCodeSet? codes;

    internal ClientConfigGzipInflate(ClientConfigGzipBitInput input)
    {
        this.input = input ?? throw new ArgumentNullException(nameof(input));
    }

    internal bool IsFinished => mode == InflateMode.Finished;
    internal bool IsTruncated { get; private set; }

    internal int Inflate(Span<byte> output)
    {
        var written = 0;
        if (IsTruncated) return written;
        try
        {
            while (true)
            {
                switch (mode)
                {
                case InflateMode.Header:
                    ReadHeader(input);
                    mode = InflateMode.Block;
                    break;
                case InflateMode.Block:
                    ReadBlock();
                    break;
                case InflateMode.Codes:
                    ReadSymbol();
                    break;
                case InflateMode.Stored:
                    if (pendingLength == 0) { FinishBlock(); break; }
                    if (written == output.Length) return written;
                    Emit(input.ReadAlignedByte(), output, ref written);
                    pendingLength--;
                    break;
                case InflateMode.Literal:
                    if (written == output.Length) return written;
                    Emit((byte)pendingLiteral, output, ref written);
                    mode = InflateMode.Codes;
                    break;
                case InflateMode.Match:
                    if (pendingLength == 0) { mode = InflateMode.Codes; break; }
                    if (written == output.Length) return written;
                    Emit(history[(historyOffset - pendingDistance) & 32767], output, ref written);
                    pendingLength--;
                    break;
                case InflateMode.Trailer:
                    ReadTrailer();
                    break;
                case InflateMode.Finished:
                    return written;
                default:
                    throw new InvalidOperationException("Unknown configuration inflate state.");
                }
            }
        }
        catch (InvalidDataException) when (input.HasExhausted)
        {
            // The target gz reader retains produced bytes on input exhaustion
            // (its buffer-error state), unlike fatal header/code/CRC failures.
            // The requested-length owner still refuses unless it obtains all
            // requested bytes; it never treats this state as whole-stream proof.
            IsTruncated = true;
            return written;
        }
    }

    private void ReadBlock()
    {
        finalBlock = input.ReadBits(1) != 0;
        switch (input.ReadBits(2))
        {
            case 0:
                input.AlignToByte();
                pendingLength = input.ReadUInt16LittleEndian();
                if ((pendingLength ^ input.ReadUInt16LittleEndian()) != 0xFFFF)
                    throw new InvalidDataException("Configuration deflate stored length is invalid.");
                mode = InflateMode.Stored;
                break;
            case 1:
                codes = ClientConfigDeflateCodeSet.Fixed;
                mode = InflateMode.Codes;
                break;
            case 2:
                codes = ClientConfigDeflateCodeSet.ReadDynamic(input);
                mode = InflateMode.Codes;
                break;
            default:
                throw new InvalidDataException("Configuration deflate block type is invalid.");
        }
    }

    private void FinishBlock() => mode = finalBlock ? InflateMode.Trailer : InflateMode.Block;

    private void Emit(byte value, Span<byte> output, ref int written)
    {
        output[written++] = value;
        checksum = ClientConfigGzipChecksum.Add(checksum, value);
        memberSize = unchecked(memberSize + 1);
        history[historyOffset] = value;
        historyOffset = (historyOffset + 1) & 32767;
        if (historyCount < history.Length) historyCount++;
    }
}
