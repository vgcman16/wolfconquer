using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCapturedCharacterPrefixTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("windows-1252")]
    public void OnlineSuccessorChangesOnlyExistingValueAndPreservesEncodingAndEveryOtherByte(string codec)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = codec == "windows-1252" ? Encoding.GetEncoding(1252) : new UTF8Encoding(codec == "utf8-bom", true);
        var text = CompleteText().Replace("Name=SqlStateSynthetic", "Name=René", StringComparison.Ordinal)
            .Replace("Online=1\r\n", "  oNlInE = \t+7 \t\n; retained\r", StringComparison.Ordinal)
            .Replace("Sentinel=owned-sql-state\r\n", "Sentinel=café €\r\nOnline=unchanged", StringComparison.Ordinal);
        var expectedText = text.Replace("  oNlInE = \t+7 \t\n", "  oNlInE = \t1 \t\n", StringComparison.Ordinal);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(expectedText)).ToArray();
        var captured = Capture(bytes);
        Assert.Equal(7U, captured.SavedPlayerValues.Online);
        Assert.Equal(7L, captured.Snapshot.Online);
        Assert.Equal("+7", captured.Snapshot.Attributes["Online"]);
        Assert.Equal("René", captured.Snapshot.Name);
        Assert.Equal(expected, captured.CopyOnlineCharacterBytes());
        Assert.Equal(bytes, captured.Image.CopyCharacterBytes());
        Assert.EndsWith("Online=unchanged", encoding.GetString(captured.CopyOnlineCharacterBytes()), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingInputRefusesWithoutInventingAnEmptyPrefix()
    {
        Assert.False(LegacyCapturedCharacterPrefix.TryCreate(null, out var captured, out var reason));
        Assert.Null(captured);
        Assert.Equal("capturedPrefixImageMissing", reason);
    }
}
