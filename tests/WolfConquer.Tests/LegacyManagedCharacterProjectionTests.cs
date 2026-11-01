using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyManagedCharacterProjectionTests
{
    private const string Image = "[Character]\r\nUID=1\r\nName=One\r\nBody=1003\r\nFace=1\r\nHaire=2\r\nClass=10\r\nFirstClass=0\r\nSecoundeClass=0\r\nReborn=0\r\nLevel=1\r\nExperience=0\r\nMap=1002\r\nX=10\r\nY=20\r\nPMap=1000\r\nPMapX=30\r\nPMapY=40\r\nStrength=1\r\nAgility=2\r\nVitaliti=3\r\nSpirit=4\r\nAtributes=5\r\nMinHitPoints=100\r\nMinMana=20\r\nMaxMana=30\r\nMoney=50\r\nConquerPoints=0\r\nBoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=0\r\nExpBallUsed=0\r\nVigor=9999\r\nFoo=upper\nfoo=lower\r\n[PaidNobility]\r\nRank=Serf";

    private const string DefaultCompositeLine = "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n";

    [Fact]
    public void ProjectionPreservesOpaqueBytesBomAndVigorWhileUpdatingOwnedValues()
    {
        var encoding = new UTF8Encoding(true, true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(Image)).ToArray();
        var source = CharacterSnapshotParser.ParseText(Image);
        var target = source with { Money = 51, MinMana = 21, MaxMana = 31 };
        var expected = Image.Replace("Money=50", "Money=51", StringComparison.Ordinal)
            .Replace("MinMana=20", "MinMana=21", StringComparison.Ordinal)
            .Replace("MaxMana=30", "MaxMana=31", StringComparison.Ordinal)
            .Replace("[PaidNobility]", DefaultCompositeLine + "[PaidNobility]", StringComparison.Ordinal);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray(),
            LegacyManagedCharacterProjection.ProjectAfterImage(bytes, target));
        var fields = new Dictionary<string, string>(source.Attributes, StringComparer.Ordinal)
            { ["Foo"] = "upper", ["foo"] = "lower", ["Vigor"] = "0" };
        var mirrored = new LegacyManagedCharacterImage(target with { Attributes = fields })
            .WithSavedMirror(encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray());
        Assert.Equal("upper", mirrored.Attributes["Foo"]);
        Assert.Equal("lower", mirrored.Attributes["foo"]);
        Assert.Equal("0", mirrored.Attributes["Vigor"]);
        Assert.Equal("51", mirrored.Attributes["Money"]);
    }

    [Fact]
    public void ProjectionUsesLegacyPhoenixStorageAlias()
    {
        var text = Image.Replace("Vigor=9999", "Pheonix=7\r\nVigor=9999", StringComparison.Ordinal);
        var source = CharacterSnapshotParser.ParseText(text);
        var fields = new Dictionary<string, string>(source.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Pheonix"] = "8" };
        var expected = text.Replace("Pheonix=7", "Pheonix=8", StringComparison.Ordinal)
            .Replace("[PaidNobility]", DefaultCompositeLine + "[PaidNobility]", StringComparison.Ordinal);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), LegacyManagedCharacterProjection.ProjectAfterImage(
            Encoding.UTF8.GetBytes(text), source with { Attributes = fields }));
    }

    [Theory]
    [InlineData("Money=50", "Money=50\r\nmoney=51")]
    [InlineData("Vigor=9999", "Vigor=9999\r\nvigor=0")]
    [InlineData("Money=50", "broken-field")]
    public void ProjectionRejectsAmbiguousOrMalformedOwnedInput(string before, string replacement)
    {
        var source = CharacterSnapshotParser.ParseText(Image);
        var invalid = Encoding.UTF8.GetBytes(Image.Replace(before, replacement, StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => LegacyManagedCharacterProjection.ProjectAfterImage(invalid, source));
    }

    [Fact]
    public void ProjectionRejectsMalformedStoredScalarBeforeReplacingIt()
    {
        var source = CharacterSnapshotParser.ParseText(Image);
        var invalid = Encoding.UTF8.GetBytes(Image.Replace("Money=50", "Money=broken", StringComparison.Ordinal));
        Assert.Throws<FormatException>(() => LegacyManagedCharacterProjection.ProjectAfterImage(invalid, source));
    }
}
