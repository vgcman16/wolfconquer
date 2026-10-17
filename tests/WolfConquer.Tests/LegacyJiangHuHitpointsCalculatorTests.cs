using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyJiangHuHitpointsCalculatorTests
{
    [Fact]
    public void CalculateSumsOnlyActiveStageMaxLifeStars()
    {
        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(
            [
                Stage(
                    active: true,
                    Star(type: 1, level: 1),
                    Star(type: 2, level: 1),
                    Star(type: 1, level: 2)),
                Stage(
                    active: false,
                    Star(type: 1, level: 3)),
            ],
            [
                Attribute(type: 1, level: 1, value: 100),
                Attribute(type: 1, level: 2, value: 200),
                Attribute(type: 1, level: 3, value: 300),
                Attribute(type: 2, level: 1, value: 900),
            ]);

        Assert.Equal(300u, contribution.RawHitpoints);
        Assert.Equal(300u, contribution.CappedHitpoints);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateAppliesLegacyConsecutiveAlignmentBonus()
    {
        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(
            [
                Stage(
                    active: true,
                    Star(type: 1, level: 1),
                    Star(type: 1, level: 2),
                    Star(type: 1, level: 3)),
            ],
            [
                Attribute(type: 1, level: 1, value: 100),
                Attribute(type: 1, level: 2, value: 200),
                Attribute(type: 1, level: 3, value: 300),
            ]);

        Assert.Equal(678u, contribution.RawHitpoints);
        Assert.Equal(678u, contribution.CappedHitpoints);
    }

    [Fact]
    public void CalculateCapsLegacyMaxLifeContributionAt12150()
    {
        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(
            [
                Stage(
                    active: true,
                    Star(type: 1, level: 1),
                    Star(type: 1, level: 2)),
            ],
            [
                Attribute(type: 1, level: 1, value: 9_000),
                Attribute(type: 1, level: 2, value: 9_000),
            ]);

        Assert.Equal(19_800u, contribution.RawHitpoints);
        Assert.Equal(12_150u, contribution.CappedHitpoints);
    }

    [Fact]
    public void CalculateReturnsZeroWhenNoMaxLifeStarsArePresent()
    {
        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(
            [Stage(active: true, Star(type: 2, level: 1))],
            [Attribute(type: 2, level: 1, value: 900)]);

        Assert.Equal(0u, contribution.RawHitpoints);
        Assert.Equal(0u, contribution.CappedHitpoints);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void TryParseLegacyRowReadsStageStarsAndAppliesStageNinePAttackMaxLifeQuirk()
    {
        var row = LegacyRow(stage9Encoded: Encoded(type: 2, level: 4));

        Assert.True(LegacyJiangHuHitpointsCalculator.TryParseLegacyRow(row, out var stages));

        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(
            stages,
            [Attribute(type: 1, level: 4, value: 450)]);
        Assert.Equal(450u, contribution.CappedHitpoints);
    }

    [Fact]
    public void CalculateFromSnapshotReportsMissingExternalRowWithoutGuessing()
    {
        var snapshot = Snapshot();

        var contribution = LegacyJiangHuHitpointsCalculator.Calculate(snapshot, catalog: null);

        Assert.Equal(0u, contribution.CappedHitpoints);
        Assert.Contains("JiangHu.txt row for character 1293600", contribution.MissingInputs);
        Assert.Contains("WorldDataCatalog.JiangHuAttributes", contribution.MissingInputs);
    }

    private static LegacyJiangHuStageState Stage(
        bool active,
        params LegacyJiangHuStarState[] stars)
    {
        return new LegacyJiangHuStageState(active, stars);
    }

    private static LegacyJiangHuStarState Star(byte type, byte level)
    {
        return new LegacyJiangHuStarState(true, Encoded(type, level));
    }

    private static ushort Encoded(byte type, byte level)
    {
        return (ushort)(type + (level * 256));
    }

    private static JiangHuAttribute Attribute(byte type, byte level, int value)
    {
        return new JiangHuAttribute(
            Id: type * 100 + level,
            AttributeType: type,
            Level: level,
            Value: value,
            SourceFile: "JianghuAttributes.txt",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static string LegacyRow(ushort stage9Encoded)
    {
        var fields = new List<string>
        {
            "1293600",
            "tester",
            "tester",
            "140",
            "3",
            "0",
            "1",
            "30000",
            "0",
        };

        for (var stage = 1; stage <= 9; stage++)
        {
            fields.Add(stage == 9 ? "1" : "0");
            for (var star = 1; star <= 9; star++)
            {
                var encoded = stage == 9 && star == 1 ? stage9Encoded : 0;
                fields.Add(encoded == 0 ? "0" : "1");
                fields.Add(encoded.ToString());
            }
        }

        return string.Join("/", fields);
    }

    private static CharacterSnapshot Snapshot()
    {
        return new CharacterSnapshot(
            Uid: 1293600,
            Name: "tester",
            Body: 1003,
            Face: 1,
            Hair: 1,
            Class: 1004,
            FirstClass: 1004,
            SecondClass: 1004,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 300,
            Y: 300,
            PreviousMapId: 1002,
            PreviousMapX: 300,
            PreviousMapY: 300,
            Strength: 1,
            Agility: 1,
            Vitality: 1,
            Spirit: 1,
            AttributePoints: 0,
            MinHitPoints: 1,
            MinMana: 1,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
