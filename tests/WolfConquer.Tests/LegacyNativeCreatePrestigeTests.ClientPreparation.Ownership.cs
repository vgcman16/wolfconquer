using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData("registered-foreign-uid")]
    [InlineData("unregistered-same-uid")]
    public void ClientPreparationGuildMemberAuthorityRefusesWithoutChangingTheRealRegisteredMember(string fault) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var guild = client.Player.MyGuild!;
            var real = client.Player.MyGuildMember!;
            Assert.Same(real, guild.Members[client.Player.UID]);
            var supplied = new LegacyNativeGuildRuntime.Member
            {
                UID = fault == "registered-foreign-uid" ? 99u : client.Player.UID,
                Name = fault == "registered-foreign-uid" ? "Other" : client.Player.Name,
                PrestigePoints = 777,
            };
            if (fault == "registered-foreign-uid")
                Assert.True(guild.Members.TryAdd(supplied.UID, supplied));
            else
                Assert.NotSame(supplied, guild.Members[client.Player.UID]);
            client.Player.MyGuildMember = supplied;
            RequirePrestigeClientPreparationRefusal(client);
            Assert.Same(real, guild.Members[client.Player.UID]);
            Assert.Equal(20_000u, real.PrestigePoints);
        });

    [Theory]
    [InlineData("chi")]
    [InlineData("inner-power")]
    [InlineData("jiang-hu")]
    public void ClientPreparationForeignUidOptionalContainerRefusesWithoutAdoptingItsPositiveScore(string owner) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            switch (owner)
            {
                case "chi":
                    client.Player.MyChi = new LegacyNativeChiRuntime(99);
                    foreach (var power in client.Player.MyChi)
                    {
                        power.UnLocked = true;
                        power.Attributes = [new(LegacyNativeChiAttribute.CriticalStrike, 200, false)];
                    }
                    break;
                case "inner-power":
                    client.Player.InnerPower = new LegacyNativeInnerPowerRuntime("Other", 99)
                        { Stages = [new() { UnLocked = true }] };
                    break;
                case "jiang-hu":
                    client.Player.MyJiangHu = new LegacyNativeJiangHu(99) { Inner_Strength = 40_500 };
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(owner));
            }
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Fact]
    public void ClientPreparationForeignBornWardrobeCannotBeAdoptedByRewritingItsPublicOwner() =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var foreign = new LegacyNativeGameClientRuntime();
            foreign.Player.UID = 99;
            var wardrobe = new LegacyNativeWardrobeRuntime(foreign);
            Assert.True(wardrobe.Items[1].TryAdd(9901, new() { Uid = 9901 }));
            wardrobe.Owner = client;
            client.MyWardrobe = wardrobe;
            RequirePrestigeClientPreparationRefusal(client);
            Assert.Same(client, wardrobe.Owner);
        });
}
