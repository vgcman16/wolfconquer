using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInnerPowerHitpointsCalculatorTests
{
    [Fact]
    public void CalculatesUnlockedGongAndCompletedStageMaxHp()
    {
        var rows = CreateRows(
            CreateRow(
                characterId: 5001,
                fields:
                [
                    "Test",
                    "5001",
                    "0",
                    "2",
                    "1",
                    "1",
                    "2",
                    "11",
                    "100",
                    "1",
                    "3",
                    "1",
                    "12",
                    "80",
                    "1",
                    "5",
                    "1",
                    "2",
                    "1",
                    "1",
                    "21",
                    "50",
                    "1",
                    "4",
                    "0",
                    "",
                ]));
        var secrets = new Dictionary<int, NeiGongSecret>
        {
            [1] = CreateSecret(1, [1, 7], [200, 999]),
            [2] = CreateSecret(2, [1], [1_000]),
        };
        var types = new Dictionary<(int SecretId, int Slot), NeiGongType>
        {
            [(1, 1)] = CreateType(secretId: 1, slot: 1, maxLevel: 5, attributeTypes: [1, 2], attributeValues: [100, 999]),
            [(1, 2)] = CreateType(secretId: 1, slot: 2, maxLevel: 5, attributeTypes: [1], attributeValues: [50]),
            [(2, 1)] = CreateType(secretId: 2, slot: 1, maxLevel: 4, attributeTypes: [1], attributeValues: [80]),
        };

        var breakdown = LegacyInnerPowerHitpointsCalculator.Calculate(5001, rows, secrets, types);

        Assert.Empty(breakdown.MissingInputs);
        Assert.Equal(390U, breakdown.Hitpoints);
    }

    [Fact]
    public void StopsAtFirstLockedOrZeroLevelLegacyBoundary()
    {
        var rows = CreateRows(
            CreateRow(
                characterId: 5001,
                fields:
                [
                    "Test",
                    "5001",
                    "0",
                    "3",
                    "1",
                    "1",
                    "2",
                    "11",
                    "100",
                    "1",
                    "2",
                    "0",
                    "12",
                    "100",
                    "0",
                    "5",
                    "1",
                    "2",
                    "0",
                    "1",
                    "21",
                    "100",
                    "1",
                    "4",
                    "1",
                    "3",
                    "1",
                    "1",
                    "31",
                    "100",
                    "1",
                    "4",
                    "1",
                    "",
                ]));
        var secrets = new Dictionary<int, NeiGongSecret>
        {
            [1] = CreateSecret(1, [1], [500]),
            [2] = CreateSecret(2, [1], [500]),
            [3] = CreateSecret(3, [1], [500]),
        };
        var types = new Dictionary<(int SecretId, int Slot), NeiGongType>
        {
            [(1, 1)] = CreateType(secretId: 1, slot: 1, maxLevel: 5, attributeTypes: [1], attributeValues: [100]),
            [(1, 2)] = CreateType(secretId: 1, slot: 2, maxLevel: 5, attributeTypes: [1], attributeValues: [1_000]),
            [(2, 1)] = CreateType(secretId: 2, slot: 1, maxLevel: 4, attributeTypes: [1], attributeValues: [1_000]),
            [(3, 1)] = CreateType(secretId: 3, slot: 1, maxLevel: 4, attributeTypes: [1], attributeValues: [1_000]),
        };

        var breakdown = LegacyInnerPowerHitpointsCalculator.Calculate(5001, rows, secrets, types);

        Assert.Empty(breakdown.MissingInputs);
        Assert.Equal(40U, breakdown.Hitpoints);
    }

    [Fact]
    public void ReportsMissingRequiredInputsWithoutGuessing()
    {
        var breakdown = LegacyInnerPowerHitpointsCalculator.Calculate(
            characterId: 5001,
            innerPowerRowsByCharacterId: null,
            neiGongSecretsById: null,
            neiGongTypeBySecretIdAndSlot: null);

        Assert.Equal(0U, breakdown.Hitpoints);
        Assert.Contains("InnerPower.txt rows", breakdown.MissingInputs);
        Assert.Contains("NeiGongInfo.ini secret definitions", breakdown.MissingInputs);
        Assert.Contains("NeiGongInfo.ini type definitions", breakdown.MissingInputs);
    }

    [Fact]
    public void ReportsMissingCharacterRow()
    {
        var breakdown = LegacyInnerPowerHitpointsCalculator.Calculate(
            5001,
            new Dictionary<int, IReadOnlyList<InnerPowerRankEntry>>(),
            new Dictionary<int, NeiGongSecret>(),
            new Dictionary<(int SecretId, int Slot), NeiGongType>());

        Assert.Equal(0U, breakdown.Hitpoints);
        Assert.Equal(["InnerPower.txt row for character 5001"], breakdown.MissingInputs);
    }

    [Fact]
    public void EmptyCharacterRowContributesZeroWithoutMissingInputs()
    {
        var rows = CreateRows(CreateRow(5001, ["Test", "5001", "0", "0", ""]));

        var breakdown = LegacyInnerPowerHitpointsCalculator.Calculate(
            5001,
            rows,
            new Dictionary<int, NeiGongSecret>(),
            new Dictionary<(int SecretId, int Slot), NeiGongType>());

        Assert.Empty(breakdown.MissingInputs);
        Assert.Equal(0U, breakdown.Hitpoints);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<InnerPowerRankEntry>> CreateRows(params InnerPowerRankEntry[] rows)
    {
        return rows
            .GroupBy(row => row.CharacterId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<InnerPowerRankEntry>)group.ToArray());
    }

    private static InnerPowerRankEntry CreateRow(int characterId, IReadOnlyList<string> fields)
    {
        return new InnerPowerRankEntry(
            CharacterName: fields[0],
            CharacterId: characterId,
            RankingFields: fields.Skip(2).Take(Math.Max(0, fields.Count - 3)).ToArray(),
            SourceFile: "InnerPower.txt",
            LineNumber: 2,
            RawLine: string.Join('/', fields),
            Fields: fields);
    }

    private static NeiGongSecret CreateSecret(
        int id,
        IReadOnlyList<int> attributeTypes,
        IReadOnlyList<int> attributeValues)
    {
        return new NeiGongSecret(
            Id: id,
            Name: $"Secret {id}",
            AniSection: "",
            NeiGongCount: 1,
            AttributeTypes: attributeTypes,
            AttributeValues: attributeValues,
            GetWayContent: "",
            SourceFile: "NeiGongInfo.ini",
            LineNumber: 1,
            RawSectionName: id.ToString(),
            Fields: new Dictionary<string, string>());
    }

    private static NeiGongType CreateType(
        int secretId,
        int slot,
        int maxLevel,
        IReadOnlyList<int> attributeTypes,
        IReadOnlyList<int> attributeValues)
    {
        return new NeiGongType(
            SecretId: secretId,
            Slot: slot,
            TypeId: (secretId * 10) + slot,
            Name: $"Type {secretId}-{slot}",
            AniSection: "",
            MaxLevel: maxLevel,
            AttributeTypes: attributeTypes,
            AttributeValues: attributeValues,
            CultureValues: Enumerable.Repeat(1, maxLevel).ToArray(),
            Condition: "",
            RequiredLevel: 0,
            RequiredNeiGongValue: 0,
            RequiredPrerequisiteNeiGong: 0,
            RequiredItemTypeId: 0,
            SourceFile: "NeiGongInfo.ini",
            LineNumber: 1,
            RawSectionName: $"{secretId}-{slot}",
            Fields: new Dictionary<string, string>());
    }
}
