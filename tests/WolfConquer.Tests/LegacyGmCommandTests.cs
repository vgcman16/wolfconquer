using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGmCommandTests
{
    private const string OperationText = "97ce18a57294419dbaeea6d585ce4d2f";
    private static readonly Guid Operation = Guid.ParseExact(OperationText, "N");

    [Theory]
    [InlineData("@wolf help")]
    [InlineData("@WoLf HeLp")]
    [InlineData("@wolf   help   ")]
    public void HelpProducesOnlyAClosedDataCommand(string text)
    {
        var command = Parsed(text);
        Assert.Equal(LegacyGmCommandKind.Help, command.Kind);
        Assert.Null(command.Inspection);
        Assert.Null(command.KitName);
        Assert.Null(command.OperationId);
    }

    [Theory]
    [InlineData("@wolf inspect", LegacyGmInspectionSection.Overview)]
    [InlineData("@wolf inspect overview", LegacyGmInspectionSection.Overview)]
    [InlineData("@wolf inspect position", LegacyGmInspectionSection.Position)]
    [InlineData("@wolf inspect vitals", LegacyGmInspectionSection.Vitals)]
    [InlineData("@wolf inspect inventory", LegacyGmInspectionSection.Inventory)]
    [InlineData("@wolf inspect equipment", LegacyGmInspectionSection.Equipment)]
    [InlineData("@wolf inspect spells", LegacyGmInspectionSection.Spells)]
    [InlineData("@wolf inspect proficiencies", LegacyGmInspectionSection.Proficiencies)]
    [InlineData("@wolf inspect quests", LegacyGmInspectionSection.Quests)]
    [InlineData("@wolf inspect mail", LegacyGmInspectionSection.Mail)]
    [InlineData("@WOLF   InSpEcT   InVeNtOrY  ", LegacyGmInspectionSection.Inventory)]
    public void InspectUsesExplicitTypedSectionWithoutAnyTarget(string text, LegacyGmInspectionSection expected)
    {
        var command = Parsed(text);
        Assert.Equal(LegacyGmCommandKind.Inspect, command.Kind);
        Assert.Equal(expected, command.Inspection);
        Assert.Null(command.KitName);
        Assert.Null(command.OperationId);
    }

    [Theory]
    [InlineData("basic-trojan")]
    [InlineData("socket-materials")]
    [InlineData("consumables")]
    public void KitRetainsExactOperationAndNormalizesOnlyClosedRecipeName(string kit)
    {
        var command = Parsed("@WOLF KIT " + kit.ToUpperInvariant() + " " + OperationText.ToUpperInvariant());
        Assert.Equal(LegacyGmCommandKind.Kit, command.Kind);
        Assert.Null(command.Inspection);
        Assert.Equal(kit, command.KitName);
        Assert.Equal(Operation, command.OperationId);
        var request = LegacyOperatorTestKits.Create(command.OperationId!.Value,
            Guid.Parse("5ea475af-f3ef-48b1-848e-e29dd06bcb87"), 2, 1293600, command.KitName!);
        Assert.Equal(Operation, request.OperationId);
        Assert.NotEmpty(request.Items);
        Assert.Equal(command.OperationId, Parsed("@wolf kit " + kit + " " + OperationText).OperationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello world")]
    [InlineData("@wolfish help")]
    [InlineData("@Wolfx inspect")]
    [InlineData("@wolf-kit consumables")]
    [InlineData("@wolf.help")]
    [InlineData(" @wolf help")]
    [InlineData("你好")]
    public void OtherChatDoesNotBecomeReservedOrCarryACommand(string? text)
    {
        var result = LegacyGmCommandParser.Parse(text);
        Assert.Equal(LegacyGmCommandParseStatus.NotReserved, result.Status);
        Assert.Equal(LegacyGmCommandError.None, result.Error);
        Assert.Null(result.Command);
    }

    [Theory]
    [InlineData("@wolf", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf   ", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf help 2", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf inspect inventory 2", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf inspect unknown", LegacyGmCommandError.UnknownInspection)]
    [InlineData("@wolf kit", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf kit consumables", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf kit consumables 97ce18a57294419dbaeea6d585ce4d2f 2", LegacyGmCommandError.InvalidSyntax)]
    [InlineData("@wolf kit arbitrary 97ce18a57294419dbaeea6d585ce4d2f", LegacyGmCommandError.UnknownKit)]
    [InlineData("@wolf heal", LegacyGmCommandError.UnknownCommand)]
    [InlineData("@wolf teleport 1002 400 400", LegacyGmCommandError.UnknownCommand)]
    [InlineData("@wolf grant 410009", LegacyGmCommandError.UnknownCommand)]
    [InlineData("@wolf inspect \"inventory\"", LegacyGmCommandError.UnknownInspection)]
    public void MalformedReservedGrammarRefusesWithoutReturningAPartialCommand(string text, LegacyGmCommandError expected) =>
        Rejected(text, expected);

    [Theory]
    [InlineData("@wolf\thelp")]
    [InlineData("@wolf\nhelp")]
    [InlineData("@wolf\rhelp")]
    [InlineData("@wolf\fhelp")]
    [InlineData("@wolf\vhelp")]
    [InlineData("@wolf help\n")]
    [InlineData("@wolf inspect\tinventory")]
    [InlineData("@wolf inspect inventóry")]
    [InlineData("@wolf \0help")]
    [InlineData("@wolf \u007fhelp")]
    public void ReservedControlsAndNonAsciiArgumentsRefuseBeforeTokenInterpretation(string text) =>
        Rejected(text, LegacyGmCommandError.InvalidCharacters);

    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("97ce18a5-7294-419d-baee-a6d585ce4d2f")]
    [InlineData("{97ce18a5-7294-419d-baee-a6d585ce4d2f}")]
    [InlineData("97ce18a57294419dbaeea6d585ce4d2")]
    [InlineData("97ce18a57294419dbaeea6d585ce4d2ff")]
    [InlineData("97ce18a57294419dbaeea6d585ce4d2z")]
    [InlineData("not-a-retained-operation")]
    public void KitRequiresNonzeroExactNGuidAndNeverSubstitutesANewOne(string operation) =>
        Rejected("@wolf kit consumables " + operation, LegacyGmCommandError.InvalidOperationId);

    [Fact]
    public void ReservedCharacterBoundaryIsBoundedWhileUnrelatedLongChatStaysUnreserved()
    {
        var exact = "@wolf help".PadRight(LegacyGmCommandParser.MaximumCharacters);
        Assert.Equal(LegacyGmCommandKind.Help, Parsed(exact).Kind);
        Rejected(exact + " ", LegacyGmCommandError.TooLong);
        Rejected("@wolf\t" + new string('a', 251), LegacyGmCommandError.TooLong);
        var other = LegacyGmCommandParser.Parse(new string('a', 1000));
        Assert.Equal(LegacyGmCommandParseStatus.NotReserved, other.Status);
        Assert.Null(other.Command);
    }

    [Fact]
    public void ReturnedErrorsNeverIncludeSuppliedRawTextOrAnOperationValue()
    {
        var result = LegacyGmCommandParser.Parse("@wolf inspect secret-account-name");
        Assert.Equal(LegacyGmCommandError.UnknownInspection, result.Error);
        Assert.Null(result.Command);
        Assert.Equal("UnknownInspection", result.Error.ToString());
    }

    private static LegacyGmCommand Parsed(string text)
    {
        var result = LegacyGmCommandParser.Parse(text);
        Assert.Equal(LegacyGmCommandParseStatus.Parsed, result.Status);
        Assert.Equal(LegacyGmCommandError.None, result.Error);
        return Assert.IsType<LegacyGmCommand>(result.Command);
    }

    private static void Rejected(string text, LegacyGmCommandError error)
    {
        var result = LegacyGmCommandParser.Parse(text);
        Assert.Equal(LegacyGmCommandParseStatus.Rejected, result.Status);
        Assert.Equal(error, result.Error);
        Assert.Null(result.Command);
    }
}
