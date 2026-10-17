using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(240)]
    [InlineData(237)]
    public async Task DirectAzureBaseDbReplacementRetiresOnlyItsOwnOldClock(int replacementFlag)
    {
        // Synthetic DB-selected status branch, not a claim that supplied21730
        // happens to contain either status. Acquisition is the real Pirate UseRune.
        await using var fixture = new DirectAzureLifeFixture(jade: true,
            origin: DateTimeOffset.UtcNow, jadeStatus: replacementFlag);
        await fixture.PrimeAsync();
        var oldBaseDeadline = fixture.Target.Character.Attributes["LegacyLightningShieldExpiresAtUtc"];
        var expires = DateTimeOffset.Parse(oldBaseDeadline, CultureInfo.InvariantCulture, DateTimeStyles.None);
        fixture.Advance(expires - fixture.Origin - TimeSpan.FromSeconds(5));
        await fixture.HitAsync();
        var oldActivatedDeadline = fixture.Target.Character.Attributes[DirectAzureLifeDeadline];
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);

        await fixture.ReplaceThroughUseRuneAsync();
        Assert.Contains(ParseSpellAnimations(fixture.Order.ToArray().Select(frame => frame.Bytes)),
            packet => packet.AttackerUid == TargetPlayerUid && packet.SpellId == 21_730);
        Assert.Equal("1", fixture.Target.Character.Attributes["DirectAzureLifeDbPower"]);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        if (replacementFlag == 240)
        {
            Assert.False(fixture.Target.Character.Attributes.ContainsKey("LegacyLightningShieldExpiresAtUtc"));
            Assert.Equal(oldActivatedDeadline, fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
            Assert.Equal("true", fixture.Target.Character.Attributes["LegacyStatusFlagRemoveOnDead.240"]);
        }
        else
        {
            Assert.Equal(oldBaseDeadline, fixture.Target.Character.Attributes["LegacyLightningShieldExpiresAtUtc"]);
            Assert.False(fixture.Target.Character.Attributes.ContainsKey(DirectAzureLifeDeadline));
        }

        var start = fixture.Order.Count;
        await fixture.StartRuntimeAsync();
        await fixture.AdvanceRuntimeAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(replacementFlag == 240,
            HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, LegacyLightningShieldActivatedFlag));
        Assert.Equal("19600", fixture.Target.Character.Attributes["LightningShieldLeft"]);
        Assert.Empty(DirectAzureLifeEntries(fixture.OwnerFramesSince(start)));
        if (replacementFlag == 240)
        {
            Assert.Empty(fixture.OwnerFramesSince(start));
            Assert.Empty(fixture.Order.ToArray().Skip(start));
            Assert.Equal(oldActivatedDeadline, fixture.Target.Character.Attributes[DirectAzureLifeDeadline]);
        }
        else
        {
            Assert.True(DirectAzureBaseVector(Assert.Single(fixture.OwnerFramesSince(start)), false, true));
            Assert.True(DirectAzureBaseVector(Assert.Single(fixture.Order.ToArray().Skip(start),
                frame => frame.Recipient == PlayerUid).Bytes, false, true));
        }
        // Five seconds elapsed after a DB duration11 registration. The old30-day
        // timer cannot expire it; generic DB-duration ownership remains held.
    }
}
