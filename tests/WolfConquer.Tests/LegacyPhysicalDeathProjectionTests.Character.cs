using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPhysicalDeathProjectionTests
{
    [Fact]
    public void DeathChangesHpAndRetainsOtherSavedScalarsAndOpaqueContext()
    {
        var before = CharacterBytes(); var original = before.ToArray();
        var after = Denied().ProjectCharacter(before);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(after, out _));
        Assert.Equal(0, saved.MinHitPoints); Assert.Equal(45, saved.MinMana);
        Assert.Equal(987, saved.Money); Assert.Equal(65, saved.ConquerPoints);
        Assert.Equal(1002, saved.MapId); Assert.Equal(430, saved.X); Assert.Equal(380, saved.Y);
        Assert.Equal("+0007", saved.Attributes["DynamicID"]);
        Assert.Equal("retain é", saved.Attributes["Opaque"]);
        Assert.True(after.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Contains("; preserve é\r\n", Encoding.UTF8.GetString(after), StringComparison.Ordinal);
        Assert.Contains("[Other]\r\nUID=777\r\nOpaque=tail\r\n", Encoding.UTF8.GetString(after), StringComparison.Ordinal);
        Assert.Equal(original, before);
    }

    [Theory]
    [InlineData(0L)] [InlineData(-1L)] [InlineData(2147483648L)]
    public void ADeathRecipeRequiresAnAliveRepresentableNativeBeforeHp(long hp) =>
        Assert.Throws<InvalidDataException>(() => Denied().ProjectCharacter(CharacterBytes(hp: hp)));

    [Theory]
    [InlineData("UID=1293600\r\n")]
    [InlineData("[Character]\r\nUID=1293600\r\n")]
    public void DuplicateCharacterAuthorityIsRefusedEvenWithMatchingValues(string extra) =>
        Assert.Throws<InvalidDataException>(() => Denied().ProjectCharacter(CharacterBytes(extra)));

    [Fact]
    public void RecordedOwnedGrantAndHpDeathProjectIntoTheSameCompleteCharacterImage()
    {
        var before = CharacterBytes("ManagedDerivedSpellOrigins=21930:7\r\n", classCode: 7051);
        var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(21_930, 7, 0, 0, 0, 0)]));
        var expected = CharacterBytes("ManagedDerivedSpellOrigins=21930:1\r\n", hp: 0, classCode: 7051);
        Assert.Equal(expected, Ranked(Tenth(42)).ProjectCharacter(before, spells));
        Assert.Equal(CharacterBytes("ManagedDerivedSpellOrigins=21930:7\r\n", classCode: 7051), before);
    }

    [Fact]
    public void RecordedOwnedRankGrantCannotOutliveItsEligibleClass()
    {
        var before = CharacterBytes("ManagedDerivedSpellOrigins=21930:1\r\n");
        var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(21_930, 1, 0, 0, 0, 0)]));
        var operation = new LegacyPhysicalDeathOperation(Uid, true, false, EmptyItems, [],
            [new WorldPacketHandlers.OwnedPhysicalDeathDerivedSpellChange(21_930, null)], []);
        Assert.Equal(CharacterBytes("ManagedDerivedSpellOrigins=\r\n", hp: 0), operation.ProjectCharacter(before, spells));
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
        Assert.Equal(new byte[4], operation.ProjectSpells(spells, character));
    }
}
