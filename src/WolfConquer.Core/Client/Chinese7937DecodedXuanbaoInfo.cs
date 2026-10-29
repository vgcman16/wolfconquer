using System.Security.Cryptography;
using System.Text;

namespace WolfConquer.Core.Client;

/// <summary>Finite selected GBK literal/lookup relationship only. Never evaluates Lua or binds runtime state.</summary>
public static partial class Chinese7937DecodedXuanbaoInfo
{
    public const int MaximumBytes = 65_536;
    public const int MaximumTokens = 32_768;
    public const int MaximumIdentifierLength = 128;
    public const int MaximumFields = 1_024;
    public const string TableName = "XuanBaoEchoInfo";
    public const string GetterName = "GetXuanBaoEchoInfo";
    public const string GlobalAccessorName = "XuanBao_GetXuanBaoEchoInfo";
    private const string ModuleName = "XuanBao";

    public static bool TryParseDecoded(ReadOnlySpan<byte> decodedGbk, Chinese7937XuanbaoInfoVersion declaredVersion,
        out Chinese7937XuanbaoInfoSnapshot? snapshot, out Chinese7937XuanbaoInfoFailure failure)
    {
        snapshot = null;
        if (!Supported(declaredVersion)) { failure = Chinese7937XuanbaoInfoFailure.UnsupportedVersion; return false; }
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
            var fields = new SelectedReader(text).Parse();
            snapshot = new(declaredVersion, Convert.ToHexString(SHA256.HashData(captured)).ToLowerInvariant(), fields);
            failure = Chinese7937XuanbaoInfoFailure.None;
            return true;
        }
        catch (Exception error) when (error is DecoderFallbackException or EncoderFallbackException)
        { failure = Chinese7937XuanbaoInfoFailure.InvalidGbk; return false; }
        catch (SelectedRefusal error) { failure = error.Failure; return false; }
    }

    private static bool Supported(Chinese7937XuanbaoInfoVersion version) =>
        version is Chinese7937XuanbaoInfoVersion.Catalog7931 or Chinese7937XuanbaoInfoVersion.Catalog7937;

    private sealed class SelectedRefusal(Chinese7937XuanbaoInfoFailure failure) : Exception
    {
        internal Chinese7937XuanbaoInfoFailure Failure { get; } = failure;
    }

    private static void Refuse(Chinese7937XuanbaoInfoFailure failure) => throw new SelectedRefusal(failure);
}
