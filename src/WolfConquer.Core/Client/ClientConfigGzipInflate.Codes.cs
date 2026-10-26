namespace WolfConquer.Core.Client;

internal sealed partial class ClientConfigGzipInflate
{
    private static readonly int[] LengthBase =
        [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];
    private static readonly int[] LengthExtra =
        [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];
    private static readonly int[] DistanceBase =
        [1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577];
    private static readonly int[] DistanceExtra =
        [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];

    private void ReadSymbol()
    {
        var symbol = codes!.Literals.Decode(input);
        if (symbol < 256)
        {
            pendingLiteral = symbol;
            mode = InflateMode.Literal;
            return;
        }

        if (symbol == 256) { FinishBlock(); return; }
        if (symbol is < 257 or > 285) throw new InvalidDataException("Configuration deflate length symbol is invalid.");
        var index = symbol - 257;
        pendingLength = LengthBase[index] + input.ReadBits(LengthExtra[index]);
        var distanceSymbol = codes.Distances.Decode(input);
        if ((uint)distanceSymbol >= DistanceBase.Length)
            throw new InvalidDataException("Configuration deflate distance symbol is invalid.");
        pendingDistance = DistanceBase[distanceSymbol] + input.ReadBits(DistanceExtra[distanceSymbol]);
        if (pendingDistance > historyCount)
            throw new InvalidDataException("Configuration deflate distance exceeds the available member history.");
        mode = InflateMode.Match;
    }
}
