using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    [Theory]
    [InlineData("hp-above-max")]
    [InlineData("critical-below-min")]
    [InlineData("none-type")]
    [InlineData("undefined-type")]
    public void ClientPreparationOwnedUnlockedChiRequiresDeclaredTypeAndActualAttributeRange(string fault) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            var chi = new LegacyNativeChiRuntime(client.Player.UID);
            client.Player.MyChi = chi;
            var attribute = fault switch
            {
                // Actual HPAdd maximum3500; CriticalStrike minimum10.
                "hp-above-max" => new LegacyNativeChiRuntime.ChiAttribute(LegacyNativeChiAttribute.HPAdd, ushort.MaxValue, false),
                "critical-below-min" => new LegacyNativeChiRuntime.ChiAttribute(LegacyNativeChiAttribute.CriticalStrike, 0, false),
                "none-type" => new LegacyNativeChiRuntime.ChiAttribute(LegacyNativeChiAttribute.None, 50, false),
                "undefined-type" => new LegacyNativeChiRuntime.ChiAttribute((LegacyNativeChiAttribute)999, 50, false),
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            chi.Dragon.UnLocked = true;
            chi.Dragon.Attributes = [attribute];
            RequirePrestigeClientPreparationRefusal(client);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClientPreparationNullOrOwnedLockedChiRetainsItsZeroContribution(bool present) =>
        RunPrestigeClientPreparation(fixture =>
        {
            var client = SeedPrestigeClientPreparation(fixture);
            SetNextPrestigeClientInputs(client);
            // Real locked powers retain default None/zero attributes; the
            // calculation must not demand unlocked metadata from these rows.
            client.Player.MyChi = present ? new LegacyNativeChiRuntime(client.Player.UID) : null;
            var owner = client.Player.MyChi;
            client.CreatePrestigePoints();
            Assert.Same(owner, client.Player.MyChi);
            Assert.Equal(0u, client.PrestigePoints[1]);
            Assert.Equal(40_000u, client.MyPrestigePoints);
            Assert.Equal(40_000u, client.Player.MyGuildMember!.PrestigePoints);
        });
}
