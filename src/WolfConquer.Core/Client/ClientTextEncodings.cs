using System.Text;

namespace WolfConquer.Core.Client;

public static class ClientTextEncodings
{
    public static Encoding GetEncoding(ClientTextEncodingKind kind)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return kind switch
        {
            ClientTextEncodingKind.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            ClientTextEncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true),
            ClientTextEncodingKind.CodePage936 => Encoding.GetEncoding(
                936,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown client text encoding.")
        };
    }
}
