using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    [Fact]
    public void PreparedDataHydratesAllSixNonemptyImagesWithoutReserializingTheirOpaqueSourceBytes()
    {
        var before = CompleteBundle(); var plan = Plan(before); var data = plan.ReadPreparedData();
        Assert.Same(plan.After, data.Source); Assert.NotSame(before, data.Source); AssertCompleteData(data);
        Assert.Equal(Encoding.UTF8.GetBytes(CharacterText().Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)), data.Source.Core.CopyCharacterBytes());
        Assert.Equal(before.Core.CopyItemBytes(), data.Source.Core.CopyItemBytes());
        for (int index = 0; index < 4; index++) Assert.Equal(before.Sidecars[index].CopyBytes(), data.Source.Sidecars[index].CopyBytes());
        var spells = SqlWorldStateBundleTests.Sidecar(data.Source, SqlWorldStateSidecarKind.Spells).CopyBytes();
        Assert.Equal(0x5A, spells[15]); Assert.Equal(0xA5, spells[27]);
        var modeled = PlayerSpellSnapshotLoader.ToLegacyBytes(data.Spells);
        Assert.Equal(0, modeled[15]); Assert.False(spells.AsSpan().SequenceEqual(modeled)); // Typed rows do not own opaque padding.
        AssertQuests(data.Character.PlayerQuests);
        Assert.Empty(plan.PreparedPrefix.Snapshot.PlayerQuests.Quests); // Only the complete projection hydrates this sidecar.
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("windows-1252")]
    public void CompleteDataPreservesSupportedCharacterEncodingAndEveryRawSidecar(string codec)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = codec == "windows-1252" ? Encoding.GetEncoding(1252) : new UTF8Encoding(codec == "utf8-bom", true);
        var text = CharacterText().Replace("Name=SqlStateSynthetic", "Name=René", StringComparison.Ordinal);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var before = CompleteBundle(bytes); var plan = Plan(before); var data = plan.ReadPreparedData();
        Assert.Equal("René", data.Character.Name); AssertCompleteData(data);
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal))).ToArray();
        Assert.Equal(expected, data.Source.Core.CopyCharacterBytes()); Assert.Equal(bytes, before.Core.CopyCharacterBytes());
        for (int index = 0; index < 4; index++) Assert.Equal(before.Sidecars[index].CopyBytes(), data.Source.Sidecars[index].CopyBytes());
    }
}
