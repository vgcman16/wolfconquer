using System.Globalization;
using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeHairfaceStorageTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeHairfaceStorageTableTests
{
    [Fact]
    public void NativeRowsKeepExactMutableFieldNamesWidthsAndImplicitNullDefaults()
    {
        var row = new Table.Hairface();
        var fields = typeof(Table.Hairface).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(new[] { "Type", "ID", "Name", "RareLevel", "RequiredVIPLevel", "Cost", "Classes", "Sex", "EquippedColor", "Colors" },
            fields.Select(field => field.Name));
        Assert.Equal(new[] { typeof(LegacyHairFaceStorageType), typeof(uint), typeof(string), typeof(byte), typeof(byte),
            typeof(uint), typeof(List<byte>), typeof(byte), typeof(byte), typeof(byte[]) },
            fields.Select(field => field.FieldType));
        Assert.Equal(LegacyHairFaceStorageType.Hairstyle, row.Type);
        Assert.Equal(0u, row.ID);
        Assert.Equal((byte)0, row.RareLevel);
        Assert.Equal((byte)0, row.RequiredVIPLevel);
        Assert.Equal(0u, row.Cost);
        Assert.Equal((byte)0, row.Sex);
        Assert.Equal((byte)0, row.EquippedColor);
        Assert.Null(row.Name);
        Assert.Null(row.Classes);
        Assert.Null(row.Colors);
        var color = new Table.HairColor();
        Assert.Equal(new[] { "ID", "Cost", "Color" },
            typeof(Table.HairColor).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(field => field.Name));
        Assert.Equal(0u, color.ID);
        Assert.Equal(0u, color.Cost);
        Assert.Equal((byte)0, color.Color);
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyHairFaceStorageType)));
        Assert.Equal(new byte[] { 0, 1 }, Enum.GetValues<LegacyHairFaceStorageType>().Select(value => (byte)value));
    }

    [Fact]
    public void NativeListsRemainReplaceableReferencesWithSameMutableRowAliases()
    {
        using var scope = new FileScope();
        var row = new Table.Hairface();
        var colors = new List<Table.HairColor>();
        var faces = new List<Table.Hairface> { row };
        Table.Hairfaces = faces;
        Table.HairColors = colors;
        Assert.Same(faces, Table.Hairfaces);
        Assert.Same(colors, Table.HairColors);
        row.ID = uint.MaxValue;
        Assert.Same(row, Table.Hairfaces[0]);
        Assert.Equal(uint.MaxValue, Table.Hairfaces[0].ID);
        Assert.False(typeof(Table).GetField("Hairfaces")!.IsInitOnly);
        Assert.False(typeof(Table).GetField("HairColors")!.IsInitOnly);
    }

    [Fact]
    public void BothMissingFilesLeaveExistingListsAndRowsUntouched()
    {
        using var scope = new FileScope();
        var face = new Table.Hairface { ID = 7 };
        var color = new Table.HairColor { ID = 8 };
        Table.Hairfaces.Add(face);
        Table.HairColors.Add(color);
        var faces = Table.Hairfaces;
        var colors = Table.HairColors;
        Table.Load();
        Assert.Same(faces, Table.Hairfaces);
        Assert.Same(colors, Table.HairColors);
        Assert.Same(face, Assert.Single(Table.Hairfaces));
        Assert.Same(color, Assert.Single(Table.HairColors));
    }

    [Fact]
    public void FacesLoadUsesLiteralTokenPositionsAndFreshSevenByteColorArray()
    {
        using var scope = new FileScope();
        scope.Faces(Face());
        Table.LoadFaces();
        var row = Assert.Single(Table.Hairfaces);
        Assert.Equal(LegacyHairFaceStorageType.Avatar, row.Type);
        Assert.Equal(1002u, row.ID);
        Assert.Equal("Native Name", row.Name);
        Assert.Equal((byte)4, row.RareLevel);
        Assert.Equal((byte)6, row.RequiredVIPLevel);
        Assert.Equal(123u, row.Cost);
        Assert.Equal(new byte[] { 1, 2 }, row.Classes);
        Assert.Equal((byte)2, row.Sex);
        Assert.Equal((byte)0, row.EquippedColor);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0 }, row.Colors);
        Assert.Empty(Table.HairColors);
    }

    [Fact]
    public void FacesSplitNoneRetainsBlankNameAndEmptyClassList()
    {
        using var scope = new FileScope();
        scope.Faces(Face(name: "", classes: ""));
        Table.LoadFaces();
        var row = Assert.Single(Table.Hairfaces);
        Assert.Equal("", row.Name);
        Assert.Empty(row.Classes!);
        Assert.Equal((byte)2, row.Sex);
    }

    [Fact]
    public void DuplicateFaceIdsAppendDistinctRowsAndIndependentColorArrays()
    {
        using var scope = new FileScope();
        scope.Faces(Face(), Face(name: "Second"));
        Table.LoadFaces();
        Assert.Equal(2, Table.Hairfaces.Count);
        Assert.NotSame(Table.Hairfaces[0], Table.Hairfaces[1]);
        Assert.Equal(Table.Hairfaces[0].ID, Table.Hairfaces[1].ID);
        Assert.NotSame(Table.Hairfaces[0].Colors, Table.Hairfaces[1].Colors);
        Table.Hairfaces[0].Colors![0] = 9;
        Assert.Equal((byte)1, Table.Hairfaces[1].Colors![0]);
        Assert.Equal("Second", Table.Hairfaces[1].Name);
    }

    [Fact]
    public void SexOnlyCatchDefaultsMalformedOverflowAndMissingTokenToZero()
    {
        using var scope = new FileScope();
        scope.Faces(Face(sex: "invalid"), Face(sex: "256"), string.Join("@@", Face().Split("@@").Take(10)));
        Table.LoadFaces();
        Assert.Equal(3, Table.Hairfaces.Count);
        Assert.All(Table.Hairfaces, row => Assert.Equal((byte)0, row.Sex));
        Assert.All(Table.Hairfaces, row => Assert.Equal(7, row.Colors!.Length));
    }

    [Fact]
    public void ShortPreSexRecordFaultRetainsEarlierRowsAndOriginalListReference()
    {
        using var scope = new FileScope();
        var faces = Table.Hairfaces;
        scope.Faces(Face(), "1@@2");
        Assert.Throws<IndexOutOfRangeException>(() => Table.LoadFaces());
        Assert.Same(faces, Table.Hairfaces);
        Assert.Equal(1002u, Assert.Single(Table.Hairfaces).ID);
    }

    [Fact]
    public void RequiredVipConversionFaultIsNotCaughtOrPublished()
    {
        using var scope = new FileScope();
        scope.Faces(Face(), Face(vip: "256"));
        Assert.Throws<OverflowException>(() => Table.LoadFaces());
        Assert.Single(Table.Hairfaces);
    }

    [Fact]
    public void InvalidClassTokenEscapesBeforeSexCatchAndDoesNotPublishFaultedRow()
    {
        using var scope = new FileScope();
        scope.Faces(Face(), Face(classes: "3,broken,4", sex: "invalid"));
        Assert.Throws<FormatException>(() => Table.LoadFaces());
        Assert.Single(Table.Hairfaces);
    }

    [Fact]
    public void ClassSplitRemovesEmptyEntriesWithoutSortingOrDeduplicating()
    {
        using var scope = new FileScope();
        scope.Faces(Face(classes: ",3,,1,3,"));
        Table.LoadFaces();
        Assert.Equal(new byte[] { 3, 1, 3 }, Assert.Single(Table.Hairfaces).Classes);
    }

    [Fact]
    public void NullGlobalFacesDoesNotAffectMissingFileOrMaskEarlierParseFault()
    {
        using var scope = new FileScope();
        Table.Hairfaces = null!;
        Table.LoadFaces();
        Assert.Null(Table.Hairfaces);
        scope.Faces(Face(id: "invalid"));
        Assert.Throws<FormatException>(() => Table.LoadFaces());
        scope.Faces(Face());
        Assert.Throws<NullReferenceException>(() => Table.LoadFaces());
        Assert.Null(Table.Hairfaces);
    }

    [Fact]
    public void ColorsLoadUsesIdColorAndTokenFourCostOnly()
    {
        using var scope = new FileScope();
        scope.Colors("400@@6@@ignored@@also ignored@@1234");
        Table.LoadColors();
        var row = Assert.Single(Table.HairColors);
        Assert.Equal(400u, row.ID);
        Assert.Equal((byte)6, row.Color);
        Assert.Equal(1234u, row.Cost);
        Assert.Empty(Table.Hairfaces);
    }

    [Fact]
    public void DuplicateColorsAppendAndLaterCostFaultLeavesEarlierAdmissions()
    {
        using var scope = new FileScope();
        scope.Colors("1@@2@@x@@x@@3", "1@@2@@x@@x@@4", "9@@1@@x@@x@@bad");
        var colors = Table.HairColors;
        Assert.Throws<FormatException>(() => Table.LoadColors());
        Assert.Same(colors, Table.HairColors);
        Assert.Equal(2, colors.Count);
        Assert.Equal(new uint[] { 3, 4 }, colors.Select(row => row.Cost));
        Assert.NotSame(colors[0], colors[1]);
    }

    [Fact]
    public void LoadCommitsFacesBeforeColorFailureWithoutClearingExistingColors()
    {
        using var scope = new FileScope();
        var existing = new Table.HairColor { ID = 9 };
        Table.HairColors.Add(existing);
        scope.Faces(Face());
        scope.Colors("1@@2@@x@@x@@bad");
        Assert.Throws<FormatException>(() => Table.Load());
        Assert.Single(Table.Hairfaces);
        Assert.Same(existing, Assert.Single(Table.HairColors));
    }

    [Fact]
    public void FaceFailureStopsLoadBeforeAnyColorAdmission()
    {
        using var scope = new FileScope();
        scope.Faces(Face(vip: "invalid"));
        scope.Colors("1@@2@@x@@x@@3");
        Assert.Throws<FormatException>(() => Table.Load());
        Assert.Empty(Table.Hairfaces);
        Assert.Empty(Table.HairColors);
    }

    [Fact]
    public void RepeatedLoadAppendsBothFamiliesAndPreservesPriorRowReferences()
    {
        using var scope = new FileScope();
        scope.Faces(Face());
        scope.Colors("1@@2@@x@@x@@3");
        Table.Load();
        var face = Table.Hairfaces[0];
        var color = Table.HairColors[0];
        Table.Load();
        Assert.Equal(2, Table.Hairfaces.Count);
        Assert.Equal(2, Table.HairColors.Count);
        Assert.Same(face, Table.Hairfaces[0]);
        Assert.Same(color, Table.HairColors[0]);
    }

    [Fact]
    public void ByteConversionsKeepUnrecognizedTypeAndFullColorRange()
    {
        using var scope = new FileScope();
        scope.Faces(Face(type: "255"));
        scope.Colors("4294967295@@255@@x@@x@@4294967295");
        Table.Load();
        Assert.Equal((LegacyHairFaceStorageType)255, Assert.Single(Table.Hairfaces).Type);
        var color = Assert.Single(Table.HairColors);
        Assert.Equal(uint.MaxValue, color.ID);
        Assert.Equal(byte.MaxValue, color.Color);
        Assert.Equal(uint.MaxValue, color.Cost);
    }

    [Fact]
    public void NativeConvertAndClassParseUseCallingCurrentCulture()
    {
        using var scope = new FileScope();
        var previous = CultureInfo.CurrentCulture;
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.PositiveSign = "p";
        try
        {
            CultureInfo.CurrentCulture = custom;
            scope.Faces(Face(type: "p1", id: "p1002", vip: "p6", classes: "p1,p2", sex: "p2"));
            scope.Colors("p400@@p6@@ignored@@ignored@@p1234");
            Table.Load();
            Assert.Equal(1002u, Table.Hairfaces[0].ID);
            Assert.Equal(new byte[] { 1, 2 }, Table.Hairfaces[0].Classes);
            Assert.Equal((byte)2, Table.Hairfaces[0].Sex);
            Assert.Equal(1234u, Table.HairColors[0].Cost);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void BothLoadersUseLiveRealConfigPathAcrossCalls()
    {
        using var scope = new FileScope();
        scope.Faces(Face());
        Table.LoadFaces();
        var face = Table.Hairfaces[0];
        var second = Path.Combine(scope.Root, "second");
        Directory.CreateDirectory(second);
        File.WriteAllLines(Path.Combine(second, "hair_color_type.txt"), ["2@@3@@x@@x@@4"]);
        LegacyNativeServerConfig.DbLocation = second + Path.DirectorySeparatorChar;
        Table.LoadColors();
        Assert.Same(face, Table.Hairfaces[0]);
        Assert.Equal(2u, Assert.Single(Table.HairColors).ID);
    }

    [Fact]
    public void ExactSpawnFieldsAreIndependentMutableBooleansOnActualPlayer()
    {
        var client = new LegacyNativeGameClientRuntime();
        var player = client.Player;
        var first = typeof(LegacyNativePlayerRuntime).GetField("SpawinEffect")!;
        var second = typeof(LegacyNativePlayerRuntime).GetField("SpawinEffectSkills")!;
        Assert.Equal(typeof(bool), first.FieldType);
        Assert.Equal(typeof(bool), second.FieldType);
        Assert.False(first.IsInitOnly);
        Assert.False(second.IsInitOnly);
        Assert.False(player.SpawinEffect);
        Assert.False(player.SpawinEffectSkills);
        player.SpawinEffect = true;
        Assert.True(player.SpawinEffect);
        Assert.False(player.SpawinEffectSkills);
        player.SpawinEffectSkills = true;
        Assert.True(player.SpawinEffectSkills);
        Assert.Same(player, client.Player);
        Assert.Null(client.Beasts); // 553 field is still not native LoadCharacter birth.
    }

    private static string Face(string type = "1", string id = "1002", string name = "Native Name",
        string vip = "6", string classes = "1,2", string sex = "2")
        => string.Join("@@", new[] { type, id, "unused2", name, "4", vip, "unused6", "123", "unused8", classes, sex });

    private sealed class FileScope : IDisposable
    {
        private readonly List<Table.Hairface> _faces = Table.Hairfaces;
        private readonly List<Table.HairColor> _colors = Table.HairColors;
        private readonly string _location = LegacyNativeServerConfig.DbLocation;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-native-hairface-" + Guid.NewGuid().ToString("N"));

        public FileScope()
        {
            var faces = new List<Table.Hairface>();
            var colors = new List<Table.HairColor>();
            Directory.CreateDirectory(Root);
            Table.Hairfaces = faces;
            Table.HairColors = colors;
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        public void Faces(params string[] rows) => File.WriteAllLines(Path.Combine(Root, "hairface_storage_type.txt"), rows);
        public void Colors(params string[] rows) => File.WriteAllLines(Path.Combine(Root, "hair_color_type.txt"), rows);

        public void Dispose()
        {
            try
            {
                Table.Hairfaces = _faces;
                Table.HairColors = _colors;
                LegacyNativeServerConfig.DbLocation = _location;
            }
            finally
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
        }
    }
}
