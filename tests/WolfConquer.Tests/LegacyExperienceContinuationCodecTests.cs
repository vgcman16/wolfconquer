using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA fixtures only: none issues a live award, query or admission capability.
public sealed class LegacyExperienceContinuationCodecTests
{
    private const long Account = 77;
    private const uint Uid = 1_293_600;
    private static readonly Guid Award = Guid.ParseExact("00112233445566778899aabbccddeeff", "N");
    private const string CreditGolden = "1|00112233445566778899aabbccddeeff|77|1293600|10|90|250|340|0|0|10|0|0|-";
    private const string StepGolden = "1|00112233445566778899aabbccddeeff|77|1293600|10|90|250|240|100|0|11|1|1|-";

    [Fact]
    public void CanonicalCreditAndEachPhaseConserveTheCompleteAward()
    {
        var credit = Credit();
        Assert.Equal(CreditGolden, LegacyExperienceContinuationCodec.Encode(credit));
        Assert.Equal(credit, LegacyExperienceContinuationCodec.Decode(CreditGolden));
        var step = credit.AdvanceLevel(100);
        Assert.Equal(StepGolden, LegacyExperienceContinuationCodec.Encode(step));
        Assert.Equal(step, LegacyExperienceContinuationCodec.Decode(StepGolden));
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, step.Phase);
        var restore = step.MarkRestore(5540);
        Assert.Equal(5540U, restore.RestoreMaximum);
        var freshQuery = restore.MarkRestore(5500); // Historical max is replaceable DATA, never a runtime grant.
        Assert.Equal(restore.RemainingExperience, freshQuery.RemainingExperience);
        var ready = freshQuery.MarkReady();
        var second = ready.AdvanceLevel(200).MarkRestore(6000).MarkReady();
        Assert.Equal((ushort)12, second.CurrentLevel); Assert.Equal((ushort)2, second.StepIndex);
        Assert.Equal(300UL, second.ConsumedExperience); Assert.Equal(40UL, second.RemainingExperience);
        Assert.Equal(340UL, second.ConsumedExperience + second.RemainingExperience + second.CappedExperience);
        LegacyExperienceContinuationCodec.ValidateTransition(credit, step);
        LegacyExperienceContinuationCodec.ValidateTransition(step, restore);
        LegacyExperienceContinuationCodec.ValidateTransition(restore, freshQuery);
        LegacyExperienceContinuationCodec.ValidateTransition(freshQuery, ready);
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ValidateTransition(credit, second));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ValidateTransition(step, null));
        Assert.Throws<InvalidDataException>(() => step.MarkReady());
        Assert.Throws<InvalidDataException>(() => credit.MarkRestore(10));
    }

    [Fact]
    public void LevelCapKeepsAnExplicitAccountOfDiscardedXpAndCannotAdvanceAgain()
    {
        var capped = LegacyExperienceContinuation.CreateCredit(Account, Uid, Award, 139, 90, 250).AdvanceLevel(100);
        Assert.Equal((ushort)140, capped.CurrentLevel); Assert.Equal(0UL, capped.RemainingExperience);
        Assert.Equal(100UL, capped.ConsumedExperience); Assert.Equal(240UL, capped.CappedExperience);
        Assert.Equal(capped, LegacyExperienceContinuationCodec.Decode(LegacyExperienceContinuationCodec.Encode(capped)));
        Assert.Throws<InvalidDataException>(() => capped.MarkRestore((uint)int.MaxValue + 1));
        Assert.Throws<InvalidDataException>(() => capped.MarkRestore(100).MarkReady().AdvanceLevel(1));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuation.CreateCredit(Account, Uid, Award, 10, long.MaxValue, 1));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuation.CreateCredit(Account, Uid, Guid.Empty, 10, 0, 1));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuation.CreateCredit(0, Uid, Award, 10, 0, 1));
    }

    [Theory]
    [InlineData(0, "2")]
    [InlineData(1, "00112233445566778899AABBCCDDEEFF")]
    [InlineData(2, "077")]
    [InlineData(2, "4294967296")]
    [InlineData(3, "0")]
    [InlineData(4, "140")]
    [InlineData(5, "-1")]
    [InlineData(6, "0")]
    [InlineData(7, "341")]
    [InlineData(8, "18446744073709551615")]
    [InlineData(9, "1")]
    [InlineData(10, "11")]
    [InlineData(11, "1")]
    [InlineData(12, "3")]
    [InlineData(13, "0")]
    public void MalformedCanonicalFieldIsNotAbsenceOrAValidContinuation(int index, string value)
    {
        var fields = CreditGolden.Split('|'); fields[index] = value;
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.Decode(string.Join('|', fields)));
    }

    [Fact]
    public void RawProjectionPreservesBomOpaqueCaseVariantsCommentsAndMixedLineEndings()
    {
        const string source = "; opaque\r\n[Character]\nUID=1293600\r\nLevel=10\nExperience=90\rOther=UP\nother=low\r\n[Opaque]\r\nRaw = a;b#c";
        string scalar = source.Replace("Experience=90", "Experience=340", StringComparison.Ordinal);
        string expected = scalar.Replace("[Opaque]", "ExperienceContinuation=" + CreditGolden + "\r\n[Opaque]", StringComparison.Ordinal);
        byte[] before = Bom(source), projected = Bom(scalar);
        var saved = LegacyExperienceContinuationCodec.ProjectCharacterImage(before, projected, Account, Uid, null, Credit());
        Assert.Equal(Bom(expected), saved); Assert.Equal(Bom(source), before); Assert.Equal(Bom(scalar), projected);
        Assert.Equal(Credit(), LegacyExperienceContinuationCodec.ReadCharacterImage(saved, Account, Uid));
        Assert.Equal(Bom(scalar), LegacyExperienceContinuationCodec.ProjectCharacterImage(saved, saved, Account, Uid, Credit(), null));
        string stepScalars = expected.Replace("Level=10", "Level=11", StringComparison.Ordinal)
            .Replace("Experience=340", "Experience=240", StringComparison.Ordinal);
        var step = Credit().AdvanceLevel(100);
        var stepImage = LegacyExperienceContinuationCodec.ProjectCharacterImage(saved, Bom(stepScalars), Account, Uid, Credit(), step);
        Assert.Equal(Bom(stepScalars.Replace(CreditGolden, StepGolden, StringComparison.Ordinal)), stepImage);
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ProjectCharacterImage(stepImage, stepImage, Account, Uid, step, null));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacterImage(saved, Account + 1, Uid));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacterImage(saved, Account, Uid + 1));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacterImage(Bom(stepScalars), Account, Uid));
    }

    [Fact]
    public void RawDuplicateMarkerOrOwnedScalarIsRejectedBeforeIniCollapse()
    {
        const string prefix = "[Character]\nUID=1293600\nLevel=10\nExperience=340\n";
        string marked = prefix + "ExperienceContinuation=" + CreditGolden + "\n";
        foreach (string suffix in new[] { "experiencecontinuation=" + CreditGolden, "uid=1293600", "LEVEL=10", "experience=340", "[CHARACTER]\nOther=1" })
            Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacterImage(Bom(marked + suffix), Account, Uid));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacterImage(Bom(prefix + "ExperienceContinuation="), Account, Uid));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.Decode(new string('x', 257)));
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.Decode(CreditGolden + "|0"));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(Bom(prefix + "Other=UP\nother=low"), Account, Uid));
    }

    [Fact]
    public void HolderProjectionPreservesOpaqueComparerAndRejectsOwnedAliases()
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal) { ["Other"] = "UP", ["other"] = "low" };
        var character = OwnedEquipmentBehaviorFixture.Character(level: 10) with { Uid = Uid, Experience = 340, Attributes = raw };
        var projected = LegacyExperienceContinuationCodec.WithState(character, Account, Credit());
        Assert.Equal("UP", projected.Attributes["Other"]); Assert.Equal("low", projected.Attributes["other"]);
        Assert.False(raw.ContainsKey(LegacyExperienceContinuationCodec.Key));
        Assert.Equal(Credit(), LegacyExperienceContinuationCodec.ReadCharacter(projected, Account));
        var cleared = LegacyExperienceContinuationCodec.WithState(projected, Account, null);
        Assert.False(cleared.Attributes.ContainsKey(LegacyExperienceContinuationCodec.Key));
        Assert.Equal("UP", cleared.Attributes["Other"]); Assert.Equal("low", cleared.Attributes["other"]);
        raw["ExperienceContinuation"] = CreditGolden; raw["experiencecontinuation"] = CreditGolden;
        Assert.Throws<InvalidDataException>(() => LegacyExperienceContinuationCodec.ReadCharacter(character, Account));
    }

    private static LegacyExperienceContinuation Credit() => LegacyExperienceContinuation.CreateCredit(Account, Uid, Award, 10, 90, 250);
    private static byte[] Bom(string text) => new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
}
