using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGmCatalogItemCommandTests
{
    private const string Operation = "97ce18a57294419dbaeea6d585ce4d2f";

    [Theory]
    [InlineData("@wolf item 1 1 97ce18a57294419dbaeea6d585ce4d2f", 1U, 1)]
    [InlineData("@WOLF  ItEm  1001000  65535  97CE18A57294419DBAEEA6D585CE4D2F  ", 1001000U, 65535)]
    [InlineData("@wolf item 4294967295 2 97ce18a57294419dbaeea6d585ce4d2f", uint.MaxValue, 2)]
    [InlineData("@wolf item 0001000 0001 97ce18a57294419dbaeea6d585ce4d2f", 1000U, 1)]
    public void ItemRetainsTypedCatalogIdStackAndExactOperationWithoutAnyGrant(string text, uint id, int stack)
    {
        var result = LegacyGmCommandParser.Parse(text);
        Assert.Equal(LegacyGmCommandParseStatus.Parsed, result.Status);
        Assert.Equal(LegacyGmCommandError.None, result.Error);
        var command = Assert.IsType<LegacyGmCommand>(result.Command);
        Assert.Equal(LegacyGmCommandKind.Item, command.Kind);
        Assert.Equal(id, command.ItemId); Assert.Equal((ushort)stack, command.StackSize);
        Assert.Equal(Guid.ParseExact(Operation, "N"), command.OperationId);
        Assert.Null(command.Inspection); Assert.Null(command.KitName);
        Assert.Equal(command.OperationId, LegacyGmCommandParser.Parse(text).Command!.OperationId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("4294967296")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("0x1000000")]
    [InlineData("Sword")]
    public void ItemIdMustBePositiveUnsignedDecimal(string id) =>
        Refused($"@wolf item {id} 1 {Operation}", LegacyGmCommandError.InvalidItemId);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("65536")]
    [InlineData("4294967295")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    public void StackMustBeWithinPositiveUshortWithoutClamping(string stack) =>
        Refused($"@wolf item 1000000 {stack} {Operation}", LegacyGmCommandError.InvalidStackSize);

    [Theory]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("97ce18a5-7294-419d-baee-a6d585ce4d2f")]
    [InlineData("97ce18a57294419dbaeea6d585ce4d2z")]
    public void OperationIsRequiredAndNeverReplacedWithANewOne(string operation) =>
        Refused($"@wolf item 1000000 1 {operation}", LegacyGmCommandError.InvalidOperationId);

    [Theory]
    [InlineData("@wolf item")]
    [InlineData("@wolf item 1000000")]
    [InlineData("@wolf item 1000000 1")]
    [InlineData("@wolf item 1000000 1 97ce18a57294419dbaeea6d585ce4d2f plus=12")]
    [InlineData("@wolf item 1000000 1 97ce18a57294419dbaeea6d585ce4d2f 2399700")]
    public void MissingFieldsTargetsOrFlagsDoNotBecomePartialCommands(string text) =>
        Refused(text, LegacyGmCommandError.InvalidSyntax);

    private static void Refused(string text, LegacyGmCommandError expected)
    {
        var result = LegacyGmCommandParser.Parse(text);
        Assert.Equal(LegacyGmCommandParseStatus.Rejected, result.Status);
        Assert.Equal(expected, result.Error); Assert.Null(result.Command);
    }
}
