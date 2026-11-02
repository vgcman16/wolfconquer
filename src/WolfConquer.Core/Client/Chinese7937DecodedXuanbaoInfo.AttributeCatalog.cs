using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Client;

public static partial class Chinese7937DecodedXuanbaoInfo
{
    /// <summary>Parses selected literal/getter relationships only; never executes Lua or changes a runtime profile.</summary>
    public static bool TryParseAttributeCatalog(ReadOnlySpan<byte> decodedGbk, Chinese7937XuanbaoInfoVersion declaredVersion,
        out Chinese7937XuanbaoAttributeCatalog? catalog, out Chinese7937XuanbaoInfoFailure failure)
    {
        catalog = null;
        if (declaredVersion != Chinese7937XuanbaoInfoVersion.Catalog7937)
        { failure = Chinese7937XuanbaoInfoFailure.UnsupportedVersion; return false; }
        if (decodedGbk.Length is <= 0 or > MaximumBytes)
        { failure = Chinese7937XuanbaoInfoFailure.InputBounds; return false; }
        var captured = decodedGbk.ToArray();
        if (captured.AsSpan().IndexOf((byte)0) >= 0)
        { failure = Chinese7937XuanbaoInfoFailure.UnsupportedSyntax; return false; }
        if (captured.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ||
            captured.AsSpan().StartsWith(new byte[] { 0xff, 0xfe }) ||
            captured.AsSpan().StartsWith(new byte[] { 0xfe, 0xff }))
        { failure = Chinese7937XuanbaoInfoFailure.InvalidGbk; return false; }
        try
        {
            var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
            var text = encoding.GetString(captured);
            if (!encoding.GetBytes(text).AsSpan().SequenceEqual(captured))
            { failure = Chinese7937XuanbaoInfoFailure.InvalidGbk; return false; }
            var (scores, flags) = new SelectedReader(text).ParseAttributeCatalog();
            catalog = new(Convert.ToHexString(SHA256.HashData(captured)).ToLowerInvariant(), scores, flags);
            failure = Chinese7937XuanbaoInfoFailure.None;
            return true;
        }
        catch (Exception error) when (error is DecoderFallbackException or EncoderFallbackException)
        { failure = Chinese7937XuanbaoInfoFailure.InvalidGbk; return false; }
        catch (SelectedRefusal error) { failure = error.Failure; return false; }
    }
}
