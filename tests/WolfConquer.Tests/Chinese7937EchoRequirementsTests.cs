using System.Collections;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoRequirementsTests
{
    private static Dictionary<string, uint> Fields(uint maximum = 5)
    {
        var fields = new Dictionary<string, uint>(StringComparer.Ordinal)
        { ["max_hole_num"] = maximum, ["cost_num"] = 123 };
        for (uint hole = 1; hole <= 7; hole++)
        {
            var suffix = hole.ToString(CultureInfo.InvariantCulture);
            fields.Add("cost_money_" + suffix, hole * 100);
            fields.Add("cost_itemtype_" + suffix, hole * 100 + 1);
            fields.Add("cost_itemnum_" + suffix, hole * 100 + 2);
            if (hole == 1) continue;
            var prefix = "open_hole" + suffix + "_";
            fields.Add(prefix + "cost_num", hole * 100 + 3);
            fields.Add(prefix + "itemtype_1", hole * 100 + 4);
            fields.Add(prefix + "exp_1", hole * 100 + 5);
            fields.Add(prefix + "itemtype_2", hole * 100 + 6);
            fields.Add(prefix + "exp_2", hole * 100 + 7);
            fields.Add(prefix + "maxexp", hole * 100 + 8);
        }
        return fields;
    }

    private static byte[] Module(IEnumerable<KeyValuePair<string, uint>> fields)
    {
        var text = new StringBuilder("local XuanBaoEchoInfo = {\n");
        foreach (var field in fields)
            text.Append("[\"").Append(field.Key).Append("\"] = ")
                .Append(field.Value.ToString(CultureInfo.InvariantCulture)).Append(",\n");
        text.Append("}\nlocal function GetXuanBaoEchoInfo(key)\nreturn XuanBaoEchoInfo[key]\nend\n")
            .Append("local function Unload()\nXuanBaoEchoInfo = nil\nend\n")
            .Append("local XuanBao = {\nGetXuanBaoEchoInfo = GetXuanBaoEchoInfo,\nUnload = Unload,\n}\n")
            .Append("XuanBao_GetXuanBaoEchoInfo = XuanBao.GetXuanBaoEchoInfo\n");
        return ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936).GetBytes(text.ToString());
    }

    private static Chinese7937XuanbaoInfoSnapshot Parse(IEnumerable<KeyValuePair<string, uint>> fields,
        Chinese7937XuanbaoInfoVersion version = Chinese7937XuanbaoInfoVersion.Catalog7937)
    {
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryParseDecoded(Module(fields), version,
            out var info, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoInfoSnapshot>(info);
    }

    private static Chinese7937EchoRequirements Create(Chinese7937XuanbaoInfoSnapshot info)
    {
        Assert.True(Chinese7937EchoRequirements.TryCreate(info, out var requirements, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoRequirementsFailure.None, failure);
        return Assert.IsType<Chinese7937EchoRequirements>(requirements);
    }

    [Fact]
    public void CompleteParsedInfoProjectsEveryRawFieldAndKeepsExactSource()
    {
        var info = Parse(Fields());
        var requirements = Create(info);
        Assert.Same(info, requirements.Source);
        Assert.Equal(59, requirements.Source.Count);
        Assert.Equal(5U, requirements.CatalogMaximumHoleNumber);
        Assert.Equal(123U, requirements.CostNumberField);
        Assert.Equal(7, requirements.CostRows.Count);
        Assert.Equal(6, requirements.OpeningRows.Count);
        for (uint hole = 1; hole <= 7; hole++)
        {
            Assert.True(requirements.TryGetCost(hole, out var cost));
            Assert.Equal(new Chinese7937EchoCostRow(hole, hole * 100, hole * 100 + 1, hole * 100 + 2), cost);
            Assert.Equal(cost, requirements.CostRows[(int)hole - 1]);
            if (hole == 1) continue;
            Assert.True(requirements.TryGetOpening(hole, out var opening));
            Assert.Equal(new Chinese7937EchoOpeningRow(hole, hole * 100 + 3, hole * 100 + 4,
                hole * 100 + 5, hole * 100 + 6, hole * 100 + 7, hole * 100 + 8), opening);
            Assert.Equal(opening, requirements.OpeningRows[(int)hole - 2]);
        }
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(4294967295U)]
    public void RawValuesRetainZeroAndUInt32MaximumWithoutDefaultsOrInterpretation(uint raw)
    {
        var fields = Fields();
        foreach (var name in fields.Keys.ToArray())
            if (name != "max_hole_num") fields[name] = raw;
        var requirements = Create(Parse(fields));
        Assert.Equal(raw, requirements.CostNumberField);
        foreach (var cost in requirements.CostRows)
            Assert.Equal(new Chinese7937EchoCostRow(cost.HoleNumber, raw, raw, raw), cost);
        foreach (var opening in requirements.OpeningRows)
            Assert.Equal(new Chinese7937EchoOpeningRow(opening.HoleNumber, raw, raw, raw, raw, raw, raw), opening);
    }

    [Fact]
    public void SourceOrderDoesNotChangeTypedIndexOrderAndCollectionsCannotBeMutated()
    {
        var fields = Fields();
        var requirements = Create(Parse(fields.Reverse()));
        fields.Clear();
        Assert.Equal(Enumerable.Range(1, 7).Select(value => (uint)value), requirements.CostRows.Select(row => row.HoleNumber));
        Assert.Equal(Enumerable.Range(2, 6).Select(value => (uint)value), requirements.OpeningRows.Select(row => row.HoleNumber));
        Assert.True(((IList)requirements.CostRows).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => { ((IList)requirements.CostRows)[0] = default(Chinese7937EchoCostRow); });
        Assert.Throws<NotSupportedException>(() => ((IList)requirements.OpeningRows).Clear());
        Assert.Equal(100U, requirements.CostRows[0].MoneyField);
    }

    [Fact]
    public void RetainedRowsBeyondNamedMaximumRemainDataWithoutGrantingSelectionPermission()
    {
        var requirements = Create(Parse(Fields(1)));
        Assert.Equal(1U, requirements.CatalogMaximumHoleNumber);
        Assert.True(requirements.TryGetCost(7, out var cost));
        Assert.Equal(7U, cost.HoleNumber);
        Assert.True(requirements.TryGetOpening(7, out var opening));
        Assert.Equal(7U, opening.HoleNumber);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(8U)]
    [InlineData(4294967295U)]
    public void UnknownHoleLookupReturnsNoRow(uint hole)
    {
        var requirements = Create(Parse(Fields()));
        Assert.False(requirements.TryGetCost(hole, out var cost));
        Assert.Equal(default, cost);
        Assert.False(requirements.TryGetOpening(hole, out var opening));
        Assert.Equal(default, opening);
    }

    [Fact]
    public void OpeningOneIsAbsentRatherThanSynthesized()
    {
        var requirements = Create(Parse(Fields()));
        Assert.False(requirements.TryGetOpening(1, out var row));
        Assert.Equal(default, row);
    }
}
