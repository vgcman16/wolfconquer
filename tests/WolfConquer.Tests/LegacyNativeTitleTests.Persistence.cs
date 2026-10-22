using System.Globalization;
using Metadata = WolfConquer.Core.Data.LegacyNativeTitleStorage;
using TitleType = WolfConquer.Core.World.LegacyNativeMsgTitleStorage.TitleType;
using Leaves = WolfConquer.Core.Data.LegacyNativeServerDatabasePersistence;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTitleTests
{
    [Fact]
    public Task EmptyTitleSaveIsLiteralZeroCommaAndDoesNotNeedMetadataOrMutateEquippedIds() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Metadata.Titles = null!;
            scope.Player.SpecialTitleID = 12345;
            scope.Player.SpecialWingID = 67890;
            scope.Player.SpecialTitleScore = 77;
            Assert.Equal("0,", Leaves.GetSpecialTitles(scope.Client));
            Assert.Equal(12345u, scope.Player.SpecialTitleID);
            Assert.Equal(67890u, scope.Player.SpecialWingID);
            Assert.Equal(77u, scope.Player.SpecialTitleScore);
            Untouched(scope);
        });

    [Fact]
    public Task SavePreservesListOrderDuplicatesUnknownIdsAndRawEquippedQuotientFlags() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Metadata.Titles = null!;
            scope.Player.SpecialTitles.AddRange([(TitleType)1, (TitleType)7, (TitleType)1, (TitleType)uint.MaxValue]);
            scope.Player.SpecialTitleID = 10007;
            scope.Player.SpecialWingID = 70003;
            Assert.Equal("4,1,1,7,1,1,1,4294967295,0,", Leaves.GetSpecialTitles(scope.Client));
            ListIs(scope, 1, 7, 1, uint.MaxValue);
            Untouched(scope);
        });

    [Fact]
    public Task InvalidOrZeroCountLinesLeaveExistingTitlesAndActiveFieldsUntouched() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.SpecialTitles.Add((TitleType)9);
            scope.Player.SpecialTitleID = 111;
            scope.Player.SpecialWingID = 222;
            scope.Player.SpecialTitleScore = 333;
            foreach (string? line in new string?[] { null, "", "2", "bad,1,1,", "0,1,1," })
                Leaves.LoadSpecialTitles(scope.Client, line!);
            ListIs(scope, 9);
            Assert.Equal(111u, scope.Player.SpecialTitleID);
            Assert.Equal(222u, scope.Player.SpecialWingID);
            Assert.Equal(333u, scope.Player.SpecialTitleScore);
            Untouched(scope);
        });

    [Fact]
    public Task LoadUsesByteFlagParsingAndRetainsMalformedOrUnknownTitleValuesAsZeroOrLiteralIds() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Row(7, 7, 3, 10);
            scope.Row(4001, 4001, 4, 11);
            Leaves.LoadSpecialTitles(scope.Client, "4,1,1,7,256,bad,1,4001,2,");
            ListIs(scope, 1, 7, 0, 4001);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(0u, scope.Player.SpecialWingID);
            Assert.Equal(9u, scope.Player.SpecialTitleScore);
            // The malformed title becomes0; native save flags it active because
            // the unchanged zero WingID also has quotient0 (there is no zero gate).
            Assert.Equal("4,1,1,7,0,0,1,4001,0,", Leaves.GetSpecialTitles(scope.Client));
            Untouched(scope);
        });

    [Fact]
    public Task RepeatedLoadAppendsDuplicatesAndEachSuccessfulActivationOverwritesOnlyItsOwnFields() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Row(4001, 4001, 3, 10);
            Leaves.LoadSpecialTitles(scope.Client, "2,1,1,4001,1,");
            Leaves.LoadSpecialTitles(scope.Client, "2,1,1,4001,1,");
            ListIs(scope, 1, 4001, 1, 4001);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(40010003u, scope.Player.SpecialWingID);
            Assert.Equal(9u, scope.Player.SpecialTitleScore);
            Assert.Equal("4,1,1,4001,1,1,1,4001,1,", Leaves.GetSpecialTitles(scope.Client));
        });

    [Fact]
    public Task TruncatedTitleRecordsStillConsumeDeclaredCountAndAppendDefaultZeroForMissingColumns() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            Leaves.LoadSpecialTitles(scope.Client, "3,1,1,7,");
            ListIs(scope, 1, 7, 0);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(0u, scope.Player.SpecialWingID);
            Assert.Equal("3,1,1,7,0,0,1,", Leaves.GetSpecialTitles(scope.Client));
            Untouched(scope);
        });

    [Fact]
    public Task TitleLineReadsRespectCurrentCultureSignAndSaveUsesNativeUnsignedDecimalStrings() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.PositiveSign = "p";
            CultureInfo.CurrentCulture = culture;
            scope.Row(7, 7);
            Leaves.LoadSpecialTitles(scope.Client, "p1,p7,p1,");
            ListIs(scope, 7);
            Assert.Equal(70002u, scope.Player.SpecialTitleID);
            Assert.Equal(9u, scope.Player.SpecialTitleScore);
            Assert.Equal("1,7,1,", Leaves.GetSpecialTitles(scope.Client));
        });

    [Theory]
    [InlineData(4000u, 40000003u, 222u, 9u)]
    [InlineData(4001u, 111u, 40010003u, 333u)]
    public Task ActivationClassifiesByPhysicalMetadataIdAt4001NotTheListKey(
        uint metadataId, uint title, uint wing, uint score) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Row(7, metadataId, 3);
        scope.Player.SpecialTitleID = 111;
        scope.Player.SpecialWingID = 222;
        scope.Player.SpecialTitleScore = 333;
        Leaves.LoadSpecialTitles(scope.Client, "1,7,1,");
        ListIs(scope, 7);
        Assert.Equal(title, scope.Player.SpecialTitleID);
        Assert.Equal(wing, scope.Player.SpecialWingID);
        Assert.Equal(score, scope.Player.SpecialTitleScore);
    });

    [Fact]
    public Task MetadataCompositeIdArithmeticWrapsUintAndDoesNotRepairTheSavedActiveFlag() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(7, uint.MaxValue, 99999);
            Leaves.LoadSpecialTitles(scope.Client, "1,7,1,");
            ListIs(scope, 7);
            Assert.Equal(89999u, scope.Player.SpecialWingID);
            Assert.Equal(0u, scope.Player.SpecialTitleID);
            Assert.Equal("1,7,0,", Leaves.GetSpecialTitles(scope.Client));
        });

    [Fact]
    public Task NullMetadataValueFaultOccursAfterAppendingItsTitleAndRetainsEarlierActivation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Row(1, 1);
            scope.Titles[7] = null!;
            scope.Player.SpecialTitles.Add((TitleType)9);
            scope.Player.SpecialWingID = 222;
            Assert.Throws<NullReferenceException>(() => Leaves.LoadSpecialTitles(scope.Client, "3,1,1,7,1,4001,1,"));
            ListIs(scope, 9, 1, 7);
            Assert.Equal(10002u, scope.Player.SpecialTitleID);
            Assert.Equal(222u, scope.Player.SpecialWingID);
            Assert.Equal(9u, scope.Player.SpecialTitleScore);
            Untouched(scope);
        });

    [Fact]
    public void NullClientIsOnlyDereferencedWhenTheParsedCountRequiresAStoredTitle()
    {
        Leaves.LoadSpecialTitles(null!, "0,");
        Assert.Throws<NullReferenceException>(() => Leaves.LoadSpecialTitles(null!, "1,1,1,"));
        Assert.Throws<NullReferenceException>(() => Leaves.GetSpecialTitles(null!));
    }
}
