using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySubClassHitpointsCalculatorTests
{
    [Fact]
    public void TryParseLegacyStateReadsSubClassSaveTriples()
    {
        var rawState = "6700#0#7#1#9#9#2#9#9#3#9#9#4#9#9#5#9#9#6#9#9#9#9#9#";

        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var state));

        Assert.Equal(6_700, state.StudyPoints);
        Assert.Equal(0, state.ActiveSubClass);
        Assert.Equal(7, state.Entries.Count);
        Assert.Equal(new LegacySubClassEntry(9, 9, 9), state.Entries[^1]);
    }

    [Fact]
    public void CalculateUsesLegacyWranglerPhraseMaxHitpointsFromSubProfessionIntro()
    {
        var state = new LegacySubClassState(
            StudyPoints: 6_700,
            ActiveSubClass: 0,
            Entries:
            [
                new LegacySubClassEntry(1, 9, 9),
                new LegacySubClassEntry(9, 9, 7),
            ]);

        var contribution = LegacySubClassHitpointsCalculator.Calculate(
            state,
            [SubProfession(id: 97, intro: "Max HP +800")]);

        Assert.Equal(800U, contribution.WranglerHitpoints);
        Assert.Equal(800U, contribution.Total);
        Assert.Empty(contribution.MissingInputs);
    }

    [Fact]
    public void CalculateReportsCatalogMissingWhenWranglerEntryExistsWithoutSubProfessionRows()
    {
        var snapshot = Snapshot(("SubProfInfo", "0#0#1#9#9#9#"));

        var contribution = LegacySubClassHitpointsCalculator.Calculate(snapshot);

        Assert.Equal(0U, contribution.Total);
        Assert.Equal(["WorldDataCatalog.SubProfessions"], contribution.MissingInputs);
    }

    [Fact]
    public void CalculateReturnsZeroForLiveUid1293600SubClassStateWithoutWranglerEntry()
    {
        var snapshot = Snapshot(
            ("SubProfInfo", "41000#0#0#"),
            ("SubPrInfo", "41000#0#0#"));

        var contribution = LegacySubClassHitpointsCalculator.Calculate(snapshot);

        Assert.Equal(0U, contribution.WranglerHitpoints);
        Assert.Equal(0U, contribution.Total);
        Assert.Equal(["SubProfInfo Wrangler subclass entry"], contribution.MissingInputs);
    }

    [Fact]
    public void CalculateReportsMalformedSavedStateWithoutGuessing()
    {
        var snapshot = Snapshot(("SubProfInfo", "41000#0#1#9#9#"));

        var contribution = LegacySubClassHitpointsCalculator.Calculate(snapshot);

        Assert.Equal(0U, contribution.Total);
        Assert.Equal(["parseable CharacterSnapshot.SubProfInfo"], contribution.MissingInputs);
    }

    private static SubProfessionEntry SubProfession(int id, string intro)
    {
        return new SubProfessionEntry(
            Id: id,
            Title: "Wrangler",
            Intro: intro,
            RequiredLevel: 0,
            RequiredMetempsychosis: 0,
            RequiredProfessions: [],
            Promotion: "",
            Suit: 0,
            LevelLimit: 9,
            RequiredJobLevel: null,
            EffectProportion: null,
            UpLevelEffect: null,
            UpStepEffect: null,
            UpStepScreenEffect: null,
            UpEffect: null,
            SubSpecial: null,
            SuitPower: null,
            BigClass: null,
            AccessionRoute: null,
            StepUpRoute: null,
            AnimationSection: null,
            SourceFile: "SubProfessionInfo.ini",
            LineNumber: id,
            RawSectionName: id.ToString(),
            Entries: []);
    }

    private static CharacterSnapshot Snapshot(params (string Key, string Value)[] attributes)
    {
        return new CharacterSnapshot(
            Uid: 1293600,
            Name: "tester",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 705,
            Y: 577,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "1293600.ini",
            Attributes: attributes.ToDictionary(
                attribute => attribute.Key,
                attribute => attribute.Value,
                StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }
}
