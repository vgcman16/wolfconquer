using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientTextPatchWriterTests
{
    [Fact]
    public void PreservesUnchangedBytesWhenStrictEncodingCannotRoundTripLegacyText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wolfconquer-client-text-{Guid.NewGuid():N}.lua");
        var legacyLineBytes = Encoding.ASCII.GetBytes("LEGACY-BYTES\r\n");
        var changedLineBytes = Encoding.ASCII.GetBytes("\t[\"STR_ITEM_TYPE_1\"] = \"??\",\r\n");
        var originalBytes = legacyLineBytes.Concat(changedLineBytes).ToArray();
        var originalText =
            "\uB863\r\n" +
            "\t[\"STR_ITEM_TYPE_1\"] = \"\u6d4b\u8bd5\",\r\n";
        var updatedText =
            "\uB863\r\n" +
            "\t[\"STR_ITEM_TYPE_1\"] = \"SpeedArrow\",\r\n";

        try
        {
            ClientTextPatchWriter.WritePatchedTextPreservingUnchangedBytes(
                path,
                originalBytes,
                originalText,
                updatedText,
                ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936));

            var patchedBytes = File.ReadAllBytes(path);
            Assert.Equal(legacyLineBytes, patchedBytes.Take(legacyLineBytes.Length).ToArray());
            Assert.Contains("SpeedArrow", Encoding.ASCII.GetString(patchedBytes), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
