using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeItemTypeParseTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeItemTypeParseTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ParseMetadataUsesRealOwnersLiveCacheReadsAndOneNativeConsoleCatch()
    {
        var method = typeof(LegacyNativeItemRuneMetadata).GetMethod("Parse")!;
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Equal(new[] { typeof(string) }, method.GetParameters().Select(parameter => parameter.ParameterType));
        var clause = Assert.Single(method.GetMethodBody()!.ExceptionHandlingClauses);
        Assert.Equal(ExceptionHandlingClauseOptions.Clause, clause.Flags);
        Assert.Equal(typeof(Exception), clause.CatchType);
        var instructions = Instructions(method);
        var handlerCalls = instructions.Where(instruction => instruction.Offset >= clause.HandlerOffset &&
            instruction.Offset < clause.HandlerOffset + clause.HandlerLength && instruction.Member is MethodBase).ToArray();
        Assert.Contains(handlerCalls, instruction => instruction.Member!.DeclaringType!.FullName ==
            "WolfConquer.Core.Runtime.LegacyNativeConsole" && instruction.Member.Name == "WriteLine");
        Assert.Contains(instructions, instruction => instruction.Member?.DeclaringType == typeof(LegacyNativeItemPosition) &&
            instruction.Member.Name == "ItemPosition");
        foreach (var pair in new[] { ("PurificationItems", 4), ("Accessorys", 2), ("SteedMounts", 2), ("Garments", 2) })
        {
            var accesses = instructions.Where(instruction => instruction.Code == OpCodes.Ldsfld &&
                instruction.Member?.Name == pair.Item1).ToArray();
            Assert.Equal(pair.Item2, accesses.Length);
            Assert.All(accesses, instruction => Assert.Equal(typeof(LegacyNativeItemTypeRuneMetadata), instruction.Member!.DeclaringType));
        }
    }

    [Fact]
    public void ParsePlusAllocationSplitAndThirdSlotAccessRemainBeforeNativeTryRegion()
    {
        var method = typeof(LegacyNativeItemRuneMetadata).GetMethod("Parse")!;
        var clause = Assert.Single(method.GetMethodBody()!.ExceptionHandlingClauses);
        var beforeTry = Instructions(method).Where(instruction => instruction.Offset < clause.TryOffset).ToArray();
        Assert.Contains(beforeTry, instruction => instruction.Code == OpCodes.Newarr &&
            instruction.Member == typeof(LegacyNativeItemTypePlus));
        Assert.Contains(beforeTry, instruction => instruction.Member is FieldInfo field && field.Name == "Plus");
        Assert.Contains(beforeTry, instruction => instruction.Member is MethodInfo member &&
            member.DeclaringType == typeof(string) && member.Name == "Split");
        Assert.Contains(beforeTry, instruction => instruction.Code == OpCodes.Ldelem_Ref);
        Assert.DoesNotContain(beforeTry, instruction => instruction.Member?.DeclaringType?.FullName ==
            "WolfConquer.Core.Runtime.LegacyNativeConsole");
    }

    [Fact]
    public void NullLineReplacesPlusBeforeSplitFaultEscapesTheNativeTry()
    {
        var r = Run("NullLine");
        Assert.Equal("System.NullReferenceException", r.GetProperty("ExceptionType").GetString());
        Assert.Equal(13, r.GetProperty("NewPlusLength").GetInt32());
        Assert.True(r.GetProperty("PlusReplaced").GetBoolean());
        Numbers(r.GetProperty("Fields"), ("ID", 0u), ("Class", 0u));
    }

    [Fact]
    public void ShortRecordReplacesPlusBeforeThirdSlotFaultEscapesTheNativeTry()
    {
        var r = Run("ShortRecord");
        Assert.Equal("System.IndexOutOfRangeException", r.GetProperty("ExceptionType").GetString());
        Assert.Equal(13, r.GetProperty("PlusLength").GetInt32());
        Assert.Equal(77u, r.GetProperty("Id").GetUInt32());
    }

    [Fact]
    public void FiftyTwoTokenEarlyReturnKeepsPriorFieldsButReplacesPlus()
    {
        var r = Run("FiftyTwoTokens");
        Numbers(r.GetProperty("Fields"), ("ID", 77u), ("Class", 88u));
        Assert.Equal("prior", r.GetProperty("Fields").GetProperty("Name").GetString());
        Assert.Equal(13, r.GetProperty("PlusLength").GetInt32());
    }

    [Fact]
    public void NulIdentifierEarlyReturnDoesNotParseIdOrName()
    {
        var r = Run("NulIdentifier");
        Assert.Equal(77u, r.GetProperty("Id").GetUInt32());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("Name").ValueKind);
        Assert.Equal(13, r.GetProperty("PlusLength").GetInt32());
    }

    [Fact]
    public void TrimmedEmptyAndZeroNameReturnAfterOnlyIdAndNameCommit()
    {
        var rows = Run("EmptyAndZeroName").GetProperty("Results").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("", rows[0].GetProperty("Name").GetString());
        Assert.Equal("0", rows[1].GetProperty("Name").GetString());
        Assert.All(rows, r => Numbers(r, ("ID", 100001u), ("Class", 99u)));
    }

    [Fact]
    public void CompleteRecordUsesNativeWidthsAndLeavesUnparsedStorageUntouched()
    {
        var r = Run("CompleteRecord");
        var f = r.GetProperty("Fields");
        Numbers(f, ("ID", 100001u), ("Class", 31u), ("Proficiency", 32u), ("Level", 33u),
            ("Gender", 1u), ("Strength", 34u), ("Agility", 35u), ("Type", 0u), ("GoldWorth", 36u),
            ("MaxAttack", 37u), ("MinAttack", 38u), ("PhysicalDefence", 39u), ("Frequency", 40u),
            ("Dodge", 41u), ("ItemHP", 42u), ("ItemMP", 43u), ("Durability", 44u),
            ("MagicAttack", 45u), ("MagicDefence", 46u), ("AttackRange", 47u),
            ("ConquerPointsWorth", 48u), ("Time", 49u), ("Crytical", 50u), ("SCrytical", 51u),
            ("Imunity", 52u), ("Penetration", 53u), ("Block", 54u), ("BreackTrough", 55u),
            ("ConterAction", 56u), ("StackSize", 57u), ("MetalResistance", 58u), ("WoodResistance", 59u),
            ("WaterResistance", 60u), ("FireResistance", 61u), ("EarthResistance", 62u),
            ("RuneSpellID", 4455u), ("BonusAttribute", 7u), ("Sort", 67u),
            ("PhysicalDamageIncrease", 68u), ("MagicDamageIncrease", 69u),
            ("PhysicalDamageDecrease", 70u), ("MagicDamageDecrease", 71u), ("Detoxication", 88u));
        Assert.Equal("Blade", f.GetProperty("Name").GetString());
        Assert.Equal("ninjaweapon", f.GetProperty("Description").GetString());
        Assert.True(f.GetProperty("AllowUpgradePlus").GetBoolean());
        Assert.Equal(-99, f.GetProperty("GradeKey").GetInt32());
        Assert.Equal(13, r.GetProperty("PlusLength").GetInt32());
        Assert.True(r.GetProperty("AllPlusElementsNull").GetBoolean());
    }

    [Fact]
    public void CurrentCultureControlsNumericSignsAndDescriptionLowercasing()
    {
        var r = Run("CurrentCulture");
        Numbers(r, ("Class", 31u), ("Sort", 67u));
        Assert.Equal("\u0131", r.GetProperty("Description").GetString());
    }

    [Fact]
    public void DescriptionAliasesAndPlusStoneBoundaryNamesKeepNativeTransformOrder()
    {
        var r = Run("DescriptionAndPlusNames");
        Assert.Equal(new[] { "ninjaweapon", "earring", "archerbow", "taoistbacksword" },
            r.GetProperty("Descriptions").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(new[] { "Blade", "Blade(+1)", "Blade(+9)", "Blade" },
            r.GetProperty("Names").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void NativeTypeDiscriminantAndFailedStackTryParseKeepTheirExactDefaults()
    {
        var rows = Run("TypeAndStack").GetProperty("Results").EnumerateArray().ToArray();
        Numbers(rows[0], ("Type", 0u), ("StackSize", 0u), ("Sort", 67u));
        Numbers(rows[1], ("Type", 1u), ("StackSize", 0u), ("Sort", 67u));
    }

    [Fact]
    public void ExactlyFiftySixTokensCommitPurificationLevelBeforeCaughtOffByOneFault()
    {
        var r = Run("Exactly56");
        Numbers(r.GetProperty("Fields"), ("PurificationLevel", 6u), ("PurificationMeteorNeed", 0u),
            ("RuneSpellID", 91u), ("Sort", 92u));
        Assert.Equal("ninjaweapon", r.GetProperty("Fields").GetProperty("Description").GetString());
        Assert.Equal(0, r.GetProperty("PurificationCount").GetInt32());
    }

    [Fact]
    public void PurificationPublishesSameConcreteRowAndKeepsTwoNativeExclusions()
    {
        var r = Run("PurificationSameAndExcluded");
        Assert.True(r.GetProperty("SameReference").GetBoolean());
        Assert.Equal(9u, r.GetProperty("Meteor").GetUInt32());
        Assert.Equal(1, r.GetProperty("BucketCount").GetInt32());
        Assert.False(r.GetProperty("ContainsExcludedA").GetBoolean());
        Assert.False(r.GetProperty("ContainsExcludedB").GetBoolean());
    }

    [Fact]
    public void ExistingPurificationIdKeepsOldReferenceWhileNewRowStillMutates()
    {
        var r = Run("PurificationDuplicate");
        Assert.True(r.GetProperty("KeepsFirst").GetBoolean());
        Assert.True(r.GetProperty("DistinctRows").GetBoolean());
        Numbers(r, ("FirstRune", 4455u), ("SecondRune", 991u));
    }

    [Fact]
    public void BothNativeAccessoryPositionsPublishSameThisAndForceStackOne()
    {
        var rows = Run("BothAccessoryPositions").GetProperty("Results").EnumerateArray().ToArray();
        Assert.Equal(new[] { 350001u, 370001u }, rows.Select(r => r.GetProperty("Id").GetUInt32()));
        Assert.All(rows, r => { Assert.True(r.GetProperty("SameReference").GetBoolean()); Numbers(r, ("Stack", 1u)); });
    }

    [Fact]
    public void NativeSteedAndGarmentPositionsPublishSameThisWithoutAccessoryStackOverride()
    {
        var rows = Run("SteedAndGarment").GetProperty("Results").EnumerateArray().ToArray();
        Assert.Equal(new[] { 200000u, 181305u }, rows.Select(r => r.GetProperty("Id").GetUInt32()));
        Assert.All(rows, r => { Assert.True(r.GetProperty("SameReference").GetBoolean()); Numbers(r, ("Stack", 57u)); });
    }

    [Fact]
    public void ExistingAccessorySteedAndGarmentKeysKeepTheirOldReferences()
    {
        var rows = Run("CategoryDuplicates").GetProperty("Results").EnumerateArray().ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, r => { Assert.True(r.GetProperty("KeepsFirst").GetBoolean()); Numbers(r, ("SecondRune", 991u)); });
    }

    [Fact]
    public void SevenNativeCpOverridesReplaceOnlyTheParsedWorthForTheirIds()
    {
        var rows = Run("CpOverrides").GetProperty("Results").EnumerateArray().ToArray();
        Assert.Equal(new[] { 754099u, 754999u, 753999u, 751999u, 619028u, 723723u, 3005945u },
            rows.Select(r => r.GetProperty("Id").GetUInt32()));
        Assert.Equal(new[] { 299u, 1699u, 1699u, 1699u, 489u, 7100u, 3100u },
            rows.Select(r => r.GetProperty("Cp").GetUInt32()));
    }

    [Fact]
    public void PublishedSameRowObservesRuneAndBonusCommitsBeforeCaughtSortFault()
    {
        var r = Run("PublishedBeforeSortFault");
        Assert.True(r.GetProperty("SameReference").GetBoolean());
        Numbers(r.GetProperty("Fields"), ("RuneSpellID", 4455u), ("BonusAttribute", 7u),
            ("Sort", 999u), ("PhysicalDamageIncrease", 888u));
    }

    [Fact]
    public void PurificationAdmissionSurvivesLaterNullAccessoryFaultWithoutRepair()
    {
        var r = Run("PurificationBeforeNullAccessory");
        Assert.True(r.GetProperty("SamePurification").GetBoolean());
        Assert.True(r.GetProperty("AccessoryStillNull").GetBoolean());
        Numbers(r.GetProperty("Fields"), ("StackSize", 99u), ("RuneSpellID", 900u), ("Sort", 0u));
    }

    [Fact]
    public void ByteOverflowStopsAtExactFieldAfterIdNameAndClassPartialCommit()
    {
        var f = Run("ByteOverflow").GetProperty("Fields");
        Numbers(f, ("ID", 100001u), ("Class", 31u), ("Proficiency", 7u), ("Level", 8u), ("RuneSpellID", 0u));
        Assert.Equal("Blade", f.GetProperty("Name").GetString());
    }

    [Fact]
    public void ResistanceUsesNativeUshortConversionDespiteUintStorageAndStopsLaterWrites()
    {
        var f = Run("ResistanceOverflow").GetProperty("Fields");
        Numbers(f, ("ConterAction", 56u), ("MetalResistance", 77u), ("WoodResistance", 0u),
            ("StackSize", 88u), ("RuneSpellID", 0u));
        Assert.Equal("prior", f.GetProperty("Description").GetString());
    }

    [Fact]
    public void MissingOptionalTailResetsAllFourDamageValuesAfterExistingSort()
    {
        var f = Run("ShortOptionalTail").GetProperty("Fields");
        Numbers(f, ("Sort", 67u), ("PhysicalDamageIncrease", 0u), ("MagicDamageIncrease", 0u),
            ("PhysicalDamageDecrease", 0u), ("MagicDamageDecrease", 0u));
    }

    [Fact]
    public void FullOptionalTailAcceptsAllFourNativeUshortValues()
    {
        var f = Run("FullOptionalTail").GetProperty("Fields");
        Numbers(f, ("PhysicalDamageIncrease", 65535u), ("MagicDamageIncrease", 65534u),
            ("PhysicalDamageDecrease", 65533u), ("MagicDamageDecrease", 65532u));
    }

    [Fact]
    public void EmptyDelimitedTokensAreRemovedAndFollowingColumnsShiftWithoutRepair()
    {
        var f = Run("RemovedEmptyTokens").GetProperty("Fields");
        Numbers(f, ("ID", 100001u), ("Class", 23u), ("Proficiency", 33u), ("Level", 1u), ("Gender", 34u));
    }

    [Fact]
    public void NullExistingPurificationBucketFaultsWithoutRepairOrLaterCategoryAdmission()
    {
        var r = Run("NullPurificationBucket");
        Assert.True(r.GetProperty("BucketStillNull").GetBoolean());
        Assert.Equal(0, r.GetProperty("AccessoryCount").GetInt32());
        Numbers(r.GetProperty("Fields"), ("PurificationLevel", 6u), ("PurificationMeteorNeed", 0u),
            ("StackSize", 99u), ("RuneSpellID", 900u));
    }

    [Fact]
    public void ExtraTrailingTokensDoNotAlterTheNativeConsumedFieldSet()
    {
        var f = Run("TrailingTokens").GetProperty("Fields");
        Numbers(f, ("ID", 100001u), ("Sort", 67u), ("PhysicalDamageIncrease", 68u), ("MagicDamageDecrease", 71u));
    }

    [Fact]
    public void ReparseReplacesPlusAndMutatesSameCachedReferenceWithoutMovingOldKeys()
    {
        var r = Run("ReparseSameObject");
        Assert.True(r.GetProperty("OldKeySame").GetBoolean());
        Assert.True(r.GetProperty("NewKeySame").GetBoolean());
        Assert.True(r.GetProperty("PlusChanged").GetBoolean());
        Numbers(r.GetProperty("Fields"), ("ID", 100002u), ("RuneSpellID", 991u));
    }

    private JsonElement Run(string scenario) => LegacyNativeItemTypeParseChildFixture.Run(scenario, output);

    private static void Numbers(JsonElement value, params (string Name, uint Value)[] fields)
    {
        foreach (var field in fields) Assert.Equal(field.Value, value.GetProperty(field.Name).GetUInt32());
    }

    private static List<Instruction> Instructions(MethodBase method)
    {
        // This inspects metadata only; no Parse, console, field-value read or initializer is invoked.
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
            .GroupBy(code => unchecked((ushort)code.Value)).ToDictionary(group => group.Key, group => group.First());
        var result = new List<Instruction>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var begin = offset;
            ushort value = bytes[offset++];
            if (value == 0xfe) value = (ushort)(0xfe00 | bytes[offset++]);
            var code = codes[value];
            var size = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4,
            };
            MemberInfo? member = null;
            if (code.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType)
                member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset));
            result.Add(new Instruction(begin, code, member));
            offset += size;
        }
        return result;
    }

    private sealed record Instruction(int Offset, OpCode Code, MemberInfo? Member);
}
