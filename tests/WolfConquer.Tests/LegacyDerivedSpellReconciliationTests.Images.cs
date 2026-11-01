using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDerivedSpellReconciliationTests
{
    [Theory]
    [InlineData("utf8")] [InlineData("bom")] [InlineData("legacy1252")]
    public void OriginProjectionPreservesEncodingFormattingAndEveryUnselectedByte(string format)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = format switch
        { "utf8" => new UTF8Encoding(false), "bom" => new UTF8Encoding(true), _ => Encoding.GetEncoding(1252) };
        string text = CharacterText("21930:4").Replace("DerivedData", "Démo", StringComparison.Ordinal)
            .Replace("ManagedDerivedSpellOrigins=21930:4", "\tManagedDerivedSpellOrigins = 21930:4  ", StringComparison.Ordinal);
        byte[] Bytes(string value) => encoding.GetPreamble().Concat(encoding.GetBytes(value)).ToArray();
        var before = Bytes(text); var spells = SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 4), (byte)0));
        var result = LegacyDerivedSpellReconciliation.Project(before, spells, new[] { Desired(21930, 3) });
        Assert.Equal(Bytes(text.Replace("21930:4", "21930:3", StringComparison.Ordinal)), result.CopyCharacterAfter());
        Assert.Equal(SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 3), (byte)0)), result.CopySpellsAfter());
        Assert.Equal(Bytes(text), before);
    }

    [Fact]
    public void InertDomainsRetainTrueSpellsAbsenceAsAnInputAndReturnCanonicalEmptyData()
    {
        var before = CharacterBytes();
        var result = LegacyDerivedSpellReconciliation.Project(before, null,
            new[] { Desired(21930, null), Desired(20000, null) });
        Assert.Equal(before, result.CopyCharacterAfter()); Assert.Equal(new byte[4], result.CopySpellsAfter());
        Assert.Empty(result.Reconciled.Changes); Assert.Empty(result.Reconciled.Spells.Spells);
    }

    [Fact]
    public void OutputCopiesAndSnapshotInputsCannotAliasLaterNegativeMutations()
    {
        var characterBytes = CharacterBytes(); var spellBytes = SpellBytes((Unrelated, (byte)0xA5));
        var desired = new[] { Desired(21930, 4) };
        var result = LegacyDerivedSpellReconciliation.Project(characterBytes, spellBytes, desired);
        var expectedCharacter = CharacterBytes("21930:4");
        var expectedSpells = SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 4), (byte)0));
        Array.Clear(characterBytes); Array.Clear(spellBytes); desired[0] = Desired(20000, 1);
        var escaped = result.CopyCharacterAfter(); Array.Clear(escaped);
        var escapedSpells = result.CopySpellsAfter(); Array.Clear(escapedSpells);
        Assert.Equal(expectedCharacter, result.CopyCharacterAfter()); Assert.Equal(expectedSpells, result.CopySpellsAfter());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)result.Reconciled.Character.Attributes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LegacyDerivedSpellDesiredRow>)result.Reconciled.Changes).Clear());
    }

    [Theory]
    [InlineData("reversed")] [InlineData("duplicate")] [InlineData("wrong-id")]
    [InlineData("wide-level")] [InlineData("progress")] [InlineData("mismatched-id")]
    public void InvalidDesiredDataNeverAcquiresAProjection(string corruption)
    {
        var desired = corruption switch
        {
            "reversed" => new[] { Desired(20000, 1), Desired(21930, 4) },
            "duplicate" => new[] { Desired(21930, 4), Desired(21930, 5) },
            "wrong-id" => new[] { Desired(1000, 1) }, "wide-level" => new[] { Desired(21930, 256) },
            "progress" => new[] { new LegacyDerivedSpellDesiredRow(21930, Grant(21930, 4) with { Experience = 1 }) },
            _ => new[] { new LegacyDerivedSpellDesiredRow(21930, Grant(20000, 4)) },
        };
        var character = CharacterBytes(); var spells = SpellBytes((Unrelated, (byte)0xA5));
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellReconciliation.Project(character, spells, desired));
        Assert.Equal(CharacterBytes(), character); Assert.Equal(SpellBytes((Unrelated, (byte)0xA5)), spells);
    }

    [Theory]
    [InlineData("duplicate-key")] [InlineData("duplicate-section")]
    public void AmbiguousPhysicalCharacterTextCannotPublishClaimBytes(string corruption)
    {
        string text = CharacterText("21930:4");
        text = corruption == "duplicate-key"
            ? text.Replace("ManagedDerivedSpellOrigins=21930:4", "ManagedDerivedSpellOrigins=21930:4\r\nmanagedderivedspellorigins=21930:4", StringComparison.Ordinal)
            : text + "\r\n[Character]\r\nOther=1";
        var before = Encoding.UTF8.GetBytes(text); var copy = before.ToArray();
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellReconciliation.Project(before,
            SpellBytes((Grant(21930, 4), (byte)0)), new[] { Desired(21930, null) }));
        Assert.Equal(copy, before);
    }
}
