using System.Globalization;

namespace WolfConquer.Core.Client;

public sealed record ClientTextFileClassificationResult(
    bool IsLikelyText,
    bool IsLikelyPackedOrBinary,
    int NullByteCount,
    double ControlByteRatio,
    int NewlineCount,
    double PrintableRatio,
    string Reason);

public static class ClientTextFileClassifier
{
    private const double MaxControlByteRatio = 0.02;
    private const double MinPrintableRatio = 0.85;
    private const int MinimumBytesForLineDensityCheck = 4096;
    private const int MaxAverageLineBytes = 2048;

    public static ClientTextFileClassificationResult Classify(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return Classify(bytes.AsSpan());
    }

    public static ClientTextFileClassificationResult Classify(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return Text(
                nullByteCount: 0,
                controlByteRatio: 0,
                newlineCount: 0,
                printableRatio: 1,
                "Empty files have no binary markers.");
        }

        var nullByteCount = 0;
        var controlByteCount = 0;
        var printableByteCount = 0;
        var newlineCount = 0;

        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];

            if (value == 0)
            {
                nullByteCount++;
            }

            if (IsSuspiciousControlByte(value))
            {
                controlByteCount++;
            }

            if (IsPrintableOrTextWhitespace(value))
            {
                printableByteCount++;
            }

            if (value == '\n')
            {
                newlineCount++;
            }
            else if (value == '\r' && (index + 1 >= bytes.Length || bytes[index + 1] != '\n'))
            {
                newlineCount++;
            }
        }

        var controlByteRatio = (double)controlByteCount / bytes.Length;
        var printableRatio = (double)printableByteCount / bytes.Length;

        if (nullByteCount > 0)
        {
            return PackedOrBinary(
                nullByteCount,
                controlByteRatio,
                newlineCount,
                printableRatio,
                $"Contains {nullByteCount} NUL byte(s), which are not expected in CP936/UTF-8 line text.");
        }

        if (controlByteRatio > MaxControlByteRatio)
        {
            return PackedOrBinary(
                nullByteCount,
                controlByteRatio,
                newlineCount,
                printableRatio,
                $"Control-byte ratio {FormatRatio(controlByteRatio)} exceeds {FormatRatio(MaxControlByteRatio)}.");
        }

        if (printableRatio < MinPrintableRatio)
        {
            return PackedOrBinary(
                nullByteCount,
                controlByteRatio,
                newlineCount,
                printableRatio,
                $"Printable-byte ratio {FormatRatio(printableRatio)} is below {FormatRatio(MinPrintableRatio)}.");
        }

        if (bytes.Length >= MinimumBytesForLineDensityCheck)
        {
            var averageLineBytes = (double)bytes.Length / (newlineCount + 1);
            if (averageLineBytes > MaxAverageLineBytes)
            {
                return PackedOrBinary(
                    nullByteCount,
                    controlByteRatio,
                    newlineCount,
                    printableRatio,
                    $"Only {newlineCount} newline(s) across {bytes.Length} byte(s); average line span {FormatNumber(averageLineBytes)} byte(s).");
            }
        }

        return Text(
            nullByteCount,
            controlByteRatio,
            newlineCount,
            printableRatio,
            "Byte profile is compatible with editable line-oriented CP936/UTF-8 text.");
    }

    private static ClientTextFileClassificationResult Text(
        int nullByteCount,
        double controlByteRatio,
        int newlineCount,
        double printableRatio,
        string reason)
    {
        return new ClientTextFileClassificationResult(
            IsLikelyText: true,
            IsLikelyPackedOrBinary: false,
            NullByteCount: nullByteCount,
            ControlByteRatio: controlByteRatio,
            NewlineCount: newlineCount,
            PrintableRatio: printableRatio,
            Reason: reason);
    }

    private static ClientTextFileClassificationResult PackedOrBinary(
        int nullByteCount,
        double controlByteRatio,
        int newlineCount,
        double printableRatio,
        string reason)
    {
        return new ClientTextFileClassificationResult(
            IsLikelyText: false,
            IsLikelyPackedOrBinary: true,
            NullByteCount: nullByteCount,
            ControlByteRatio: controlByteRatio,
            NewlineCount: newlineCount,
            PrintableRatio: printableRatio,
            Reason: reason);
    }

    private static bool IsSuspiciousControlByte(byte value)
    {
        return (value < 0x20 && !IsTextWhitespace(value)) || value == 0x7F;
    }

    private static bool IsPrintableOrTextWhitespace(byte value)
    {
        return IsTextWhitespace(value) || (value >= 0x20 && value <= 0x7E) || value >= 0x80;
    }

    private static bool IsTextWhitespace(byte value)
    {
        return value is (byte)'\t' or (byte)'\r' or (byte)'\n';
    }

    private static string FormatRatio(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
