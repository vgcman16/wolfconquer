using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedAlternateEquipmentUiTests
{
    // Controlled transient status on the genuine admitted bound actor. This
    // tests query ordering, not FineRain or Imp attack causation.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEquipmentAlternateUiFineRainInactiveSkipsAdmissionButSelectedRejectsAfter5(bool active)
    {
        var fixture = await SourceAlternateUiFixture.CreateAsync(active, guarded: true);
        await WithFixtureAsync(fixture, async () =>
        {
            await fixture.EquipAsync();
            var frames = fixture.OwnerFrames.ToArray();
            Assert.Equal(6, frames.Length);
            SourceAlternateUiAssertItemPrefix(frames);
            LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[4], 5, 92_602, 51);
            if (active)
            {
                Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(frames[5]), out var wait));
                Assert.Contains(wait.Content, text => text.Contains("wait 6 seconds", StringComparison.Ordinal));
            }
            else
            {
                LegacySelectedEquipmentQueryAssertions.AlternateNotification(frames[5],
                    LegacyShowEquipmentPacket.AllowAlternanteAction, 0, 0);
            }

            Assert.Empty(ParseMsgUpdates(frames));
            Assert.Empty(fixture.PeerFrames);
            Assert.Equal(active ? 1 : 0, fixture.Clock.Reads);
            Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
            Assert.Equal(600U, fixture.OwnedMaximum);
            Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
            Assert.Equal((uint?)60, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Breakthrough);
            Assert.Equal((uint?)75, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).Counteraction);
            Assert.Equal(92_602U, Assert.Single(fixture.Owner.PlayerItems.EquipmentItems,
                item => item.Position == 51).Uid);
        });
    }

}
