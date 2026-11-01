using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class Chinese7937CombatGearCatalogTests
{
    private const string ObservedFirst = "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@\r\n";
    private const string ObservedSecond = "2@@201@@2@@1200@@120@@60@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@480@@0@@0@@0@@\r\n";

    [Fact]
    public void PackedAndDecodedRoundTripPreservesEveryRawFieldAndOrder()
    {
        var leading = Row("0001", "4294967295", "00000000000000000000");
        var bytes = Encoding.ASCII.GetBytes(ObservedFirst + ObservedSecond + leading + ObservedFirst);
        var packed = ClientItemCatalogCodec.Encode(bytes);
        var document = Chinese7937CombatGearCatalogDocument.ParsePacked(packed, 7937);
        Assert.Equal(4, document.Rows.Count); // Duplicate IDs are deliberately not rejected.
        Assert.Equal(22, document.Rows[0].Values.Count);
        Assert.Equal(17_640_000U, document.Rows[0].Values[9]);
        Assert.Equal("0001", document.Rows[2].RawFields[0]);
        Assert.Equal(uint.MaxValue, document.Rows[2].Values[1]);
        Assert.Equal("00000000000000000000", document.Rows[2].RawFields[2]);
        Assert.Equal(0U, document.Rows[2].Values[2]);
        Assert.Equal(bytes, document.ToArray());
        Assert.Equal(packed, document.ToPacked());
        Array.Clear(bytes); Array.Clear(packed);
        var copy = document.ToArray(); copy[0] = (byte)'9';
        Assert.Equal((byte)'1', document.ToArray()[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<uint>)document.Rows[0].Values)[0] = 5);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)document.Rows[0].RawFields)[0] = "changed");
    }

    [Theory]
    [InlineData("empty-field")]
    [InlineData("non-digit")]
    [InlineData("signed")]
    [InlineData("overflow")]
    [InlineData("too-many-digits")]
    [InlineData("short-row")]
    [InlineData("long-row")]
    [InlineData("missing-final-delimiter")]
    [InlineData("lf")]
    [InlineData("truncated")]
    [InlineData("nul")]
    public void MalformedDecodedAndPackedInputsRefuseTheWholeDocument(string fault)
    {
        var fields = Enumerable.Repeat("0", 22).ToArray();
        switch (fault)
        {
            case "empty-field": fields[8] = ""; break;
            case "non-digit": fields[8] = "1x"; break;
            case "signed": fields[8] = "-1"; break;
            case "overflow": fields[8] = "4294967296"; break;
            case "too-many-digits": fields[8] = new string('0', 21); break;
            case "nul": fields[8] = "0\0"; break;
        }
        var line = string.Join("@@", fields) + "@@\r\n";
        line = fault switch
        {
            "short-row" => string.Join("@@", fields.Take(21)) + "@@\r\n",
            "long-row" => string.Join("@@", fields.Append("0")) + "@@\r\n",
            "missing-final-delimiter" => line[..^4] + "\r\n",
            "lf" => line[..^2] + "\n",
            "truncated" => line[..^1],
            _ => line,
        };
        var data = Encoding.ASCII.GetBytes(ObservedFirst + line);
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParseDecoded(data, 7937));
        var packed = ClientItemCatalogCodec.Encode(data);
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParsePacked(packed, 7937));
    }

    [Fact]
    public void VersionInputAndRowBoundsRefuseBeforeProducingADocument()
    {
        var row = Encoding.ASCII.GetBytes(ObservedFirst);
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParseDecoded(row, 7936));
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParsePacked([], 7937));
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParseDecoded(
            new byte[Chinese7937CombatGearCatalogDocument.MaximumBytes + 1], 7937));
        var many = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(Row("0", "0", "0"),
            Chinese7937CombatGearCatalogDocument.MaximumRows + 1)));
        Assert.Throws<InvalidDataException>(() => Chinese7937CombatGearCatalogDocument.ParseDecoded(many, 7937));
    }

    private static string Row(string first, string second, string third) =>
        string.Join("@@", new[] { first, second, third }.Concat(Enumerable.Repeat("0", 19))) + "@@\r\n";
}
