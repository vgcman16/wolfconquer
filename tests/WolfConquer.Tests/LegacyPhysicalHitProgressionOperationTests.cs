using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Pure captured DATA; no admitted player, live hit, writer or recovery owner.
public sealed class LegacyPhysicalHitProgressionOperationTests
{
    private const uint Actor = 101, Target = 202;
    private static byte[] Items(params PlayerItemSnapshotItem[] rows) =>
        PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot([], [], [], rows, []));
    private static PlayerItemSnapshotItem Weapon(uint uid, uint itemId, ushort position, ushort durability = 100) =>
        new() { Uid = uid, ItemId = itemId, Position = position, Durability = durability, MaximumDurability = 100, StackSize = 1 };
    private static byte[] Character(long hp = 123) => Encoding.UTF8.GetBytes(
        "[Character]\r\nUID=202\r\nName=Target\r\nBody=1008\r\nFace=154\r\nHaire=0\r\nClass=142\r\n" +
        "FirstClass=0\r\nSecoundeClass=0\r\nReborn=2\r\nLevel=140\r\nExperience=0\r\nMap=1002\r\nX=430\r\nY=380\r\n" +
        "PMap=1002\r\nPMapX=430\r\nPMapY=380\r\nStrength=10\r\nAgility=10\r\nVitaliti=10\r\nSpirit=10\r\nAtributes=0\r\n" +
        $"MinHitpoints={hp}\r\nMinMana=45\r\nMaxMana=45\r\nExpBallUsed=0\r\nMoney=987\r\n" +
        "ConquerPoints=65\r\nBoundConquerPoints=0\r\nBankCps=0\r\nPkPoints=0\r\nOnline=1\r\nOpaque=retain\r\n" +
        "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n");
    private static LegacyPhysicalHitProgressionOperation Create(byte[] items, uint raw = 10, uint damage = 10,
        bool alternate = false, LegacyPhysicalDeathOperation? death = null) => new(9001, Actor, 9002, Target, raw, damage, alternate, items, death);
    private static LegacyPhysicalDeathOperation Death() => new(Target, false, false,
        PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), [], [], []);

    [Fact]
    public void NonlethalRecipeDerivesOrderedWeaponsAndRetainsOpaqueExistingRowBytes()
    {
        var items = Items(Weapon(11, 410009, 4), Weapon(12, 420009, 5));
        var input = PlayerProficiencySnapshotLoader.ToLegacyBytes(new PlayerProficiencySnapshot([new(410, 1, 0, 0, 1200)]));
        input[13] = 0xA1; input[14] = 0xB2; input[15] = 0xC3;
        var op = Create(items); var original = items.ToArray(); var profOriginal = input.ToArray();
        items[0] ^= 1; // Construction retained the physical source, not this caller array.
        var result = op.Project(Character(), input);
        Assert.Equal(new uint[] { 410, 420 }, result.WeaponIds);
        var target = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(result.CopyCharacterAfter(), out _));
        Assert.Equal(113, target.MinHitPoints); Assert.Equal("retain", target.Attributes["Opaque"]);
        var output = result.CopyProficienciesAfter(); var rows = PlayerProficiencySnapshotLoader.Parse(output).Proficiencies;
        Assert.Equal(new uint[] { 410, 420 }, rows.Select(row => row.Id));
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3 }, output[13..16]);
        Assert.Equal(new PlayerProficiencySnapshotEntry(420, 0, 0, 0, 0), rows[1]);
        Assert.Equal(original, op.ActorItems); Assert.Equal(profOriginal, input);
        output[0] ^= 1; Assert.NotEqual(output, result.CopyProficienciesAfter());
        var returned = op.ActorItems; returned[0] ^= 1; Assert.Equal(original, op.ActorItems);
        Assert.Null(result.CopySpellsAfter());
    }

    [Fact]
    public void AlternateSelectionAndDuplicateTypesUseActualEquipmentSelection()
    {
        var items = Items(Weapon(11, 410009, 4), Weapon(12, 420009, 5), Weapon(13, 430009, 54), Weapon(14, 430019, 55));
        var result = Create(items, alternate: true).Project(Character(), null);
        Assert.Equal(new uint[] { 430 }, result.WeaponIds);
        Assert.Equal(new PlayerProficiencySnapshotEntry(430, 0, 0, 0, 0),
            Assert.Single(PlayerProficiencySnapshotLoader.Parse(result.CopyProficienciesAfter()).Proficiencies));
    }

    [Fact]
    public void DeathUsesTheExistingDeathRecipeAndTheSameProficiencyImage()
    {
        var death = Death(); var before = Character(); var items = Items(Weapon(11, 410009, 4));
        var result = Create(items, raw: 123, damage: 123, death: death).Project(before, null);
        Assert.Equal(death.ProjectCharacter(before), result.CopyCharacterAfter());
        var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
        Assert.Equal(death.ProjectSpells(null, parsed), result.CopySpellsAfter());
        Assert.Equal(new PlayerProficiencySnapshotEntry(410, 0, 0, 0, 0),
            Assert.Single(PlayerProficiencySnapshotLoader.Parse(result.CopyProficienciesAfter()).Proficiencies));
    }

    [Fact]
    public void ZeroAppliedDamageCanStillProduceTheRecordedNonneutralProficiencyRecipe()
    {
        var result = Create(Items(Weapon(11, 410009, 4)), raw: 10, damage: 0).Project(Character(), null);
        var target = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(result.CopyCharacterAfter(), out _));
        Assert.Equal(123, target.MinHitPoints);
        Assert.Equal((uint)410, Assert.Single(PlayerProficiencySnapshotLoader.Parse(result.CopyProficienciesAfter()).Proficiencies).Id);
    }

    [Fact]
    public void InvalidIdentityDamageOrNeutralSelectionCannotIssueADataProjection()
    {
        var items = Items(Weapon(11, 410009, 4));
        Assert.Throws<InvalidDataException>(() => new LegacyPhysicalHitProgressionOperation(1, Actor, 1, Target, 10, 5, false, items, null));
        Assert.Throws<InvalidDataException>(() => new LegacyPhysicalHitProgressionOperation(1, Actor, 2, Actor, 10, 5, false, items, null));
        Assert.Throws<InvalidDataException>(() => Create(items, raw: 0, damage: 0));
        Assert.Throws<InvalidDataException>(() => Create(items, raw: 9, damage: 10));
        Assert.Throws<InvalidDataException>(() => Create(items, raw: 123, damage: 123).Project(Character(), null));
        Assert.Throws<InvalidDataException>(() => Create(items, raw: 123, damage: 122, death: Death()).Project(Character(), null));
        Assert.Throws<InvalidDataException>(() => Create(items).Project(Encoding.UTF8.GetBytes("[Character]\r\nUID=203\r\nMap=1002\r\nX=430\r\nY=380\r\nMinHitPoints=123\r\n"), null));
        Assert.Throws<InvalidDataException>(() => Create(Items(Weapon(11, 410009, 4, 0))).Project(Character(), null));
        Assert.Throws<InvalidDataException>(() => Create(items).Project(Character(), null, new byte[4]));
        var cap = PlayerProficiencySnapshotLoader.ToLegacyBytes(new PlayerProficiencySnapshot([new(410, 20, 0, 0, 0)]));
        Assert.Throws<InvalidDataException>(() => Create(items).Project(Character(), cap));
    }

    [Theory]
    [InlineData(4294967296L)]
    [InlineData(long.MaxValue)]
    public void AccountIdsAboveTheNativeUInt32DomainAreRefusedIndependently(long invalid)
    {
        var items = Items(Weapon(11, 410009, 4));
        Assert.Throws<InvalidDataException>(() => new LegacyPhysicalHitProgressionOperation(invalid, Actor, 2, Target, 10, 5, false, items, null));
        Assert.Throws<InvalidDataException>(() => new LegacyPhysicalHitProgressionOperation(1, Actor, invalid, Target, 10, 5, false, items, null));
    }

    [Fact]
    public void TargetPendingOrMalformedContinuationCannotIssueADataProjection()
    {
        var op = Create(Items(Weapon(11, 410009, 4)));
        var state = LegacyExperienceContinuation.CreateCredit(9002, Target, Guid.NewGuid(), 139, 0, 10);
        string character = Encoding.UTF8.GetString(Character())
            .Replace("Level=140\r\nExperience=0", "Level=139\r\nExperience=10");
        var valid = Encoding.UTF8.GetBytes(character + "ExperienceContinuation=" +
            LegacyExperienceContinuationCodec.Encode(state) + "\r\n");
        var malformed = Encoding.UTF8.GetBytes(character + "ExperienceContinuation=malformed\r\n");
        Assert.Throws<InvalidDataException>(() => op.Project(valid, null));
        Assert.Throws<InvalidDataException>(() => op.Project(malformed, null));
    }

    [Fact]
    public void CompleteRawItemAndProficiencyInputsMustBeWellFormed()
    {
        var items = Items(Weapon(11, 410009, 4));
        Assert.Throws<FormatException>(() => Create(items[..^1]));
        var duplicate = PlayerProficiencySnapshotLoader.ToLegacyBytes(new PlayerProficiencySnapshot([new(410, 1, 0, 0, 0), new(410, 2, 0, 0, 0)]));
        Assert.Throws<InvalidDataException>(() => Create(items).Project(Character(), duplicate));
        Assert.Throws<InvalidDataException>(() => Create(items).Project(Character(), new byte[] { 1, 0, 0, 0 }));
    }
}
