namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("blank")]
    [InlineData("slash")]
    [InlineData("control")]
    [InlineData("invalid-unicode")]
    public void ClientPreparationMalformedPlayerNameRefusesWithoutChangingPreviouslyPublishedFields(string fault) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            client.Player.Name = fault switch
            {
                "empty" => "",
                "blank" => " ",
                "slash" => "Bad/Name",
                "control" => "Bad\nName",
                // Construct this in the test body, not an attribute UTF8 blob.
                "invalid-unicode" => "Bad\uD800",
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationNullBoundGuildNameRefusesWithoutReplacingAnyPriorOwnerOrValue() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            client.Player.MyGuild!.GuildName = null!;
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationValidUnicodeNameKeepsItsExactTextAndRegisteredGuildMember() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var member = client.Player.MyGuildMember!;
            const string name = "H\u00E9ro\U0001F409";
            client.Player.Name = name;
            client.CreatePrestigePoints();
            Assert.Equal(name, client.PrestrigeEntry!.Name);
            Assert.Equal(40_000u, client.MyPrestigePoints);
            Assert.Same(member, client.Player.MyGuild!.Members[client.Player.UID]);
            Assert.Same(member, client.Player.MyGuildMember);
            Assert.Equal(40_000u, member.PrestigePoints);
        });
}
