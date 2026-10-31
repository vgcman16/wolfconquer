using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)10950, "-1")]
    [InlineData((ushort)10950, "not-a-number")]
    [InlineData((ushort)10950, "18446744073709551616")]
    [InlineData((ushort)10950, "")]
    [InlineData((ushort)20220, "-1")]
    [InlineData((ushort)20220, "not-a-number")]
    [InlineData((ushort)20220, "18446744073709551616")]
    [InlineData((ushort)20220, "")]
    public Task OwnedProcExplicitInvalidSavedStaminaCannotBecomeDefaultAdmissionResources(ushort proc, string rawStamina) =>
        RunRejectedProcStaminaAdmission(proc, rawStamina, async f =>
        {
            // Malformed saved funds can no longer be admitted. Keep the same
            // eight proc/raw inputs and prove refusal at the real earlier gate.
            f.AssertNoAdmission(); f.Data.AssertFiles(f.BeforeLogin);
            foreach (uint uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var saved = Assert.IsType<WolfConquer.Core.Data.CharacterSnapshot>(f.Data.Loader.LoadByAccountId(uid));
                Assert.Equal(rawStamina, saved.Attributes["Stamina"]); Assert.Equal(100L, saved.MinMana);
            }
            await f.AttemptProcFrameAsync(proc);
            f.AssertNoAdmission(); f.Data.AssertFiles(f.BeforeLogin);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
