using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA-only raw projection; no journal, interaction or live owner is minted.
public sealed class LegacyPairMovementProjectionTests
{
    [Theory]
    [InlineData("utf8")] [InlineData("bom")] [InlineData("legacy1252")]
    public void OnlyChangedCoordinateDigitsChangeAndEveryOtherByteSurvives(string format)
    {
        var encoding = EncodingFor(format); string before = Character();
        var bytes = Bytes(before, encoding);
        Assert.True(LegacyPairMovementCharacterProjection.TryProject(bytes, Walk(), 1001, out var after, out var reason), reason);
        var expected = Bytes(before.Replace("\tY\t=00380\t", "\tY\t=381\t", StringComparison.Ordinal), encoding);
        Assert.Equal(expected, after); Assert.Equal(Bytes(before, encoding), bytes);
    }

    [Fact]
    public void InPlaceJumpPreservesEvenTheOriginalNumericSpellings()
    {
        var before = Bytes(Character(), new UTF8Encoding(true));
        var jump = new LegacyPairMovementOperation(LegacyPairMovementKind.Jump, 7, 1001, 9, 1002,
            1002, 0, 430, 380, 430, 380, 65535);
        Assert.True(LegacyPairMovementCharacterProjection.TryProject(before, jump, 1001, out var after, out _));
        Assert.Equal(before, after); Assert.NotSame(before, after);
    }

    [Theory]
    [InlineData(null)] [InlineData("0")] [InlineData("+00001")] [InlineData("907")]
    public void RuntimeInstanceIdentityDoesNotRewriteOptionalLegacySavedContext(string? savedDynamicId)
    {
        var text = Character();
        if (savedDynamicId is not null)
            text = text.Replace("Opaque=", "DynamicID=" + savedDynamicId + "\r\nOpaque=", StringComparison.Ordinal);
        var before = Bytes(text, new UTF8Encoding(true));
        var operation = new LegacyPairMovementOperation(LegacyPairMovementKind.Walk,
            7, 1001, 9, 1002, 1002, 907, 430, 380, 430, 381, 0);
        Assert.True(LegacyPairMovementCharacterProjection.TryProject(before, operation, 1001, out var after, out var reason), reason);
        var expected = Bytes(text.Replace("\tY\t=00380\t", "\tY\t=381\t", StringComparison.Ordinal), new UTF8Encoding(true));
        Assert.Equal(expected, after); Assert.Equal(Bytes(text, new UTF8Encoding(true)), before);
    }

    [Theory]
    [InlineData("cr")] [InlineData("lf")] [InlineData("mixed")]
    public void EveryIniSupportedLineEndingRetainsItsExactUnselectedBytes(string lineEndings)
    {
        var text = Character().Replace("\r\n", lineEndings == "lf" ? "\n" : "\r", StringComparison.Ordinal);
        if (lineEndings == "mixed")
            text = text.Replace("\r  X =", "\n  X =", StringComparison.Ordinal)
                .Replace("\r\tY\t=", "\r\n\tY\t=", StringComparison.Ordinal);
        var encoding = new UTF8Encoding(false); var before = Bytes(text, encoding);
        Assert.True(LegacyPairMovementCharacterProjection.TryProject(before, Walk(), 1001, out var after, out var reason), reason);
        Assert.Equal(Bytes(text.Replace("\tY\t=00380\t", "\tY\t=381\t", StringComparison.Ordinal), encoding), after);
        Assert.Equal(Bytes(text, encoding), before);
    }

    [Theory]
    [InlineData("duplicate-x")] [InlineData("duplicate-uid")] [InlineData("duplicate-character")]
    [InlineData("wrong-uid")] [InlineData("wrong-map")] [InlineData("wrong-source")]
    [InlineData("dynamic-overflow")] [InlineData("dynamic-malformed")]
    public void AmbiguousOrForeignRecordedInputsNeverPublishAnAfterimage(string corruption)
    {
        string text = Character();
        text = corruption switch
        {
            "duplicate-x" => text.Replace("  X = +00430  ", "X=430\r\nx=430", StringComparison.Ordinal),
            "duplicate-uid" => text.Replace("UID=1001", "UID=1001\r\nuid=1001", StringComparison.Ordinal),
            "duplicate-character" => text + "\r\n[Character]\r\nOpaque2=retained\r\n",
            "wrong-uid" => text.Replace("UID=1001", "UID=1003", StringComparison.Ordinal),
            "wrong-map" => text.Replace("Map=1002", "Map=1003", StringComparison.Ordinal),
            "wrong-source" => text.Replace("+00430", "+00431", StringComparison.Ordinal),
            "dynamic-overflow" => text.Replace("Opaque=", "DynamicID=4294967296\r\nOpaque=", StringComparison.Ordinal),
            "dynamic-malformed" => text.Replace("Opaque=", "DynamicID=bad\r\nOpaque=", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
        };
        var before = Bytes(text, new UTF8Encoding(false)); var copy = before.ToArray();
        Assert.False(LegacyPairMovementCharacterProjection.TryProject(before, Walk(), 1001, out var after, out _));
        Assert.Empty(after); Assert.Equal(copy, before);
    }

    [Theory]
    [InlineData(0, 430, 381)] [InlineData(1, 429, 381)] [InlineData(2, 429, 380)] [InlineData(3, 429, 379)]
    [InlineData(4, 430, 379)] [InlineData(5, 431, 379)] [InlineData(6, 431, 380)] [InlineData(7, 431, 381)]
    public void OperationGeometryMatchesTheIndependentSourceWalkTable(int direction, int x, int y)
    {
        var operation = new LegacyPairMovementOperation(LegacyPairMovementKind.Walk, 7, 1001, 9, 1002,
            1002, 0, 430, 380, checked((ushort)x), checked((ushort)y), checked((ushort)direction));
        operation.Validate(); Assert.Equal((uint)1001, operation.UidFor(7)); Assert.Equal((uint)1002, operation.UidFor(9));
        Assert.Throws<InvalidDataException>(() => operation.UidFor(1001));
    }

    private static LegacyPairMovementOperation Walk() => new(LegacyPairMovementKind.Walk,
        7, 1001, 9, 1002, 1002, 0, 430, 380, 430, 381, 0);

    private static Encoding EncodingFor(string format)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return format switch
        { "utf8" => new UTF8Encoding(false), "bom" => new UTF8Encoding(true), "legacy1252" => Encoding.GetEncoding(1252),
            _ => throw new ArgumentOutOfRangeException(nameof(format)) };
    }
    private static byte[] Bytes(string value, Encoding encoding) => encoding.GetPreamble().Concat(encoding.GetBytes(value)).ToArray();
    private static string Character() => string.Join("\r\n", new[]
    {
        "; opaque formatting and trailing whitespace survive", "[Character]", "UID=1001", "Name=Démo",
        "Body=1008", "Face=154", "Haire=0", "Class=7051", "FirstClass=0", "SecoundeClass=0",
        "Reborn=2", "Level=140", "Experience=0", "Map=1002", "  X = +00430  ", "\tY\t=00380\t",
        "PMap=1002", "PMapX=430", "PMapY=380", "Strength=10", "Agility=10", "Vitaliti=10", "Spirit=10",
        "Atributes=0", "MinHitPoints=1", "MinMana=50", "Money=777", "ConquerPoints=222",
        "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=1", "Opaque=a=b;#retained",
        "[Unrelated]", "X=999", "UID=42", "Sentinel=exact", "",
    });
}
