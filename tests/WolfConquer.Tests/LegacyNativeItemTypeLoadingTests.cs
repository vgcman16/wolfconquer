using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemTypeLoadingTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeItemTypeLoadingTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ServerConfigMetadataPreservesAllNativeMutableStorageInOrder()
    {
        var expected = new (string Name, Type Type)[]
        {
            ("LogginKey", typeof(string)), ("CO2Folder", typeof(string)),
            ("XtremeTopLink", typeof(string)), ("Descord", typeof(string)),
            ("ServerID", typeof(uint)), ("ConquerorWinner", typeof(uint)),
            ("IPAddres", typeof(string)), ("AuthPort", typeof(ushort)),
            ("GamePort", typeof(ushort)), ("ServerName", typeof(string)),
            ("Port_BackLog", typeof(ushort)), ("Port_ReceiveSize", typeof(ushort)),
            ("Port_SendSize", typeof(ushort)), ("DbLocation", typeof(string)),
            ("ExpRateSpell", typeof(uint)), ("ExpRateProf", typeof(uint)),
            ("UserExpRate", typeof(uint)), ("PhysicalDamage", typeof(uint)),
            ("IsInterServer", typeof(bool)),
        };
        var fields = typeof(LegacyNativeServerConfig)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(expected.Select(field => field.Name), fields.Select(field => field.Name));
        Assert.Equal(expected.Select(field => field.Type), fields.Select(field => field.FieldType));
        Assert.All(fields, field =>
        {
            Assert.False(field.IsInitOnly);
            Assert.False(field.IsLiteral);
            Assert.Equal(typeof(LegacyNativeServerConfig), field.DeclaringType);
        });
    }

    [Fact]
    public void LoadingMetadataIsParameterlessSameTableInstanceVoidWithoutCatch()
    {
        var method = typeof(LegacyNativeItemTypeRuneMetadata).GetMethod("Loading")!;
        Assert.Equal(typeof(LegacyNativeItemTypeRuneMetadata), method.DeclaringType);
        Assert.False(method.IsStatic);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Empty(method.GetParameters());
        Assert.Empty(method.GetMethodBody()!.ExceptionHandlingClauses);
        Assert.Contains(method.GetMethodBody()!.LocalVariables,
            variable => variable.LocalType == typeof(Dictionary<uint, LegacyNativeItemTypePlus[]>));
    }

    [Fact]
    public void FreshConfigOwnerHasPublicDefaultsWithoutPrivateOperationalValues()
    {
        var fields = Run("ConfigDefaults").GetProperty("Fields");
        foreach (var name in new[] { "LogginKey", "XtremeTopLink", "Descord" })
            Assert.Equal("", fields.GetProperty(name).GetString());
        Assert.Equal("Test", fields.GetProperty("ServerName").GetString());
        foreach (var name in new[] { "CO2Folder", "IPAddres", "DbLocation" })
            Assert.Equal("", fields.GetProperty(name).GetString());
        foreach (var name in new[] { "ServerID", "ConquerorWinner", "AuthPort", "GamePort", "Port_BackLog" })
            Assert.Equal(0u, fields.GetProperty(name).GetUInt32());
        foreach (var name in new[] { "Port_ReceiveSize", "Port_SendSize" })
            Assert.Equal(16384u, fields.GetProperty(name).GetUInt32());
        foreach (var name in new[] { "ExpRateSpell", "ExpRateProf", "UserExpRate" })
            Assert.Equal(1000u, fields.GetProperty(name).GetUInt32());
        Assert.Equal(100u, fields.GetProperty("PhysicalDamage").GetUInt32());
        Assert.False(fields.GetProperty("IsInterServer").GetBoolean());
        Assert.Equal(19, fields.EnumerateObject().Count());
    }

    [Fact]
    public void MainAndSubFilesPopulateTheSameRealTableWithParsedClassAndRuneFields()
    {
        var r = Run("MainAndSub");
        Number(r, "Count", 2);
        Assert.Equal("Main", r.GetProperty("MainName").GetString());
        Assert.Equal("Sub", r.GetProperty("SubName").GetString());
        Number(r, "Class", 1005);
        Number(r, "SubClass", 4001);
        Number(r, "RuneSpellID", 12345);
    }

    [Fact]
    public void SameBaseRowsShareTheActualThirteenSlotPlusArray()
    {
        var r = Run("SharedPlus");
        Assert.True(r.GetProperty("SameArray").GetBoolean());
        Assert.True(r.GetProperty("FirstAllowed").GetBoolean());
        Assert.True(r.GetProperty("SecondAllowed").GetBoolean());
        Assert.True(r.GetProperty("ZeroSlotNull").GetBoolean());
        Number(r, "Length", 13);
        Number(r, "PlusID", 111000);
        Number(r, "ItemHP", 10);
    }

    [Fact]
    public void PlusLookupPreservesBaseThenTenThenTwentyPriority()
    {
        var r = Run("PlusPriority");
        Assert.Equal(new[] { 111000u, 112010u, 113020u },
            r.GetProperty("SelectedPlusIDs").EnumerateArray().Select(value => value.GetUInt32()));
    }

    [Fact]
    public void DuplicateMainAndSubRowsStillParseIntoCachesWithoutReplacingFirstTableRow()
    {
        var r = Run("DuplicateCaches");
        Number(r, "Count", 1);
        Assert.Equal("First", r.GetProperty("FirstName").GetString());
        Assert.Equal("MainDuplicate", r.GetProperty("MainCacheName").GetString());
        Assert.Equal("SubDuplicate", r.GetProperty("SubCacheName").GetString());
        Assert.True(r.GetProperty("MainDistinct").GetBoolean());
        Assert.True(r.GetProperty("SubDistinct").GetBoolean());
        Assert.True(r.GetProperty("CachesDistinct").GetBoolean());
    }

    [Fact]
    public void MissingItemAddPropagatesBeforeAnyTableAdmission()
    {
        var r = Run("MissingItemAdd");
        Assert.Equal(typeof(FileNotFoundException).FullName, r.GetProperty("ExceptionType").GetString());
        Number(r, "Count", 0);
    }

    [Fact]
    public void ItemAddIndexFaultPrecedesTheMissingMainFileRead()
    {
        var r = Run("InvalidItemAdd");
        Assert.Equal(typeof(IndexOutOfRangeException).FullName, r.GetProperty("ExceptionType").GetString());
        Number(r, "Count", 0);
    }

    [Fact]
    public void MissingMainPropagatesAfterTheRealItemAddPhase()
    {
        var r = Run("MissingMain");
        Assert.Equal(typeof(FileNotFoundException).FullName, r.GetProperty("ExceptionType").GetString());
        Number(r, "Count", 0);
    }

    [Fact]
    public void MissingSubLeavesAlreadyAdmittedMainRowsVisible()
    {
        var r = Run("MissingSub");
        Assert.Equal(typeof(FileNotFoundException).FullName, r.GetProperty("ExceptionType").GetString());
        Number(r, "Count", 1);
        Assert.Equal("CommittedMain", r.GetProperty("Name").GetString());
    }

    [Fact]
    public void ShortAndTrimmedCommentLinesAreSkippedBeforeRowConstruction()
    {
        var r = Run("SkipShortComments");
        Number(r, "Count", 1);
        Assert.Equal("Kept", r.GetProperty("Name").GetString());
    }

    [Fact]
    public void SuccessiveLoadsRetainOldRowsButAllocateANewLocalPlusDictionary()
    {
        var r = Run("SuccessiveLoads");
        Number(r, "Count", 2);
        Assert.Equal("Original", r.GetProperty("OldName").GetString());
        Assert.True(r.GetProperty("SameOldRow").GetBoolean());
        Assert.True(r.GetProperty("SameOldPlus").GetBoolean());
        Assert.True(r.GetProperty("DifferentGeneration").GetBoolean());
        Number(r, "OldHP", 10);
        Number(r, "NewHP", 20);
    }

    [Fact]
    public void NullDbLocationUsesNativeRelativeFilenameConcatenation()
    {
        var r = Run("NullLocation");
        Number(r, "Count", 1);
        Assert.Equal("Relative", r.GetProperty("Name").GetString());
        Assert.True(r.GetProperty("LocationStillNull").GetBoolean());
    }

    [Fact]
    public void SwallowedParseFaultStillAdmitsTheSamePartiallyWrittenRow()
    {
        var r = Run("SwallowedParseFault");
        Number(r, "Count", 1);
        Assert.Equal("Partial", r.GetProperty("Name").GetString());
        Number(r, "Class", 0);
        Number(r, "RuneSpellID", 0);
        Number(r, "Sort", 0);
        Number(r, "PlusLength", 13);
    }

    private JsonElement Run(string scenario) => LegacyNativeItemTypeLoadingChildFixture.Run(scenario, output);

    private static void Number(JsonElement value, string name, uint expected) =>
        Assert.Equal(expected, value.GetProperty(name).GetUInt32());
}
