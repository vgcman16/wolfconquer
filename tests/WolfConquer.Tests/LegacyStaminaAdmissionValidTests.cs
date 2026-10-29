using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false, "100", null, 90UL)] [InlineData(true, "100", null, 90UL)]
    [InlineData(false, "+120", "0", 110UL)] [InlineData(true, "+120", "0", 110UL)]
    [InlineData(false, null, null, 90UL)] [InlineData(true, null, null, 90UL)]
    [InlineData(false, null, "1", 140UL)] [InlineData(true, null, "1", 140UL)]
    [InlineData(false, "18446744073709551615", null, 18446744073709551605UL)]
    [InlineData(true, "18446744073709551615", null, 18446744073709551605UL)]
    public Task OwnedValidOrAbsentStaminaRetainsExactAcceptedRemainderAndWireOrder(
        bool root, string? raw, string? blessing, ulong remaining) =>
        RunStaminaAdmissionOwner(raw, blessing, false, 1000, async (f, world, clock) =>
        {
            if (raw is null)
            {
                Assert.False(f.Current(PlayerUid).Character.Attributes.ContainsKey("Stamina"));
                Assert.False(f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes.ContainsKey("Stamina"));
            }
            else StaminaInput(f, raw);
            var before = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); int first = f.Frames(PlayerUid).Count;
            var prepared = await InvokeStaminaAdmission(f, world, clock, root);
            Assert.NotNull(prepared); Assert.Same(prepared, f.Current(PlayerUid)); Assert.Same(peer, f.Current(TargetPlayerUid));
            Assert.Equal(remaining.ToString(CultureInfo.InvariantCulture), prepared.Character.Attributes["Stamina"]);
            Assert.Equal(950L, prepared.Character.MinMana); Assert.Equal(950U, prepared.NativePlayer!.Mana);
            Assert.Equal(950L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
            var frames = f.Frames(PlayerUid).Skip(first).ToArray(); Assert.Equal(root ? 2 : 3, frames.Length);
            Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(PlayerUid, remaining).ToFrameBytes(), frames[0]);
            Assert.Equal(new LegacyMsgUpdatePacket(PlayerUid, [new(LegacyMsgUpdateDataType.Mana, 950)]).ToFrameBytes(), frames[1]);
            var id = root ? (ushort)1000 : (ushort)1045;
            Assert.True(prepared.MagicCooldowns!.ContainsKey(id));
            if (!root)
            {
                Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(prepared.PlayerSpells, world, id, out _, out var spell));
                Assert.Equal(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(), frames[2]);
            }
            Assert.Equal(before.Character.Money, prepared.Character.Money); Assert.Equal(before.Character.ConquerPoints, prepared.Character.ConquerPoints);
            Assert.Same(before.PlayerItems, prepared.PlayerItems); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData(false, 1039L, (byte)10)] [InlineData(true, 1039L, (byte)10)]
    [InlineData(false, 1000L, (byte)0)] [InlineData(true, 1000L, (byte)0)]
    public Task OwnedUnusedStaminaDoesNotAcquireAnUnrelatedValidationRequirement(bool root, long map, byte cost) =>
        RunStaminaAdmissionOwner("not-a-number", "1", true, map, async (f, world, clock) =>
        {
            StaminaInput(f, "not-a-number"); int first = f.Frames(PlayerUid).Count;
            var prepared = await InvokeStaminaAdmission(f, world, clock, root); Assert.NotNull(prepared);
            Assert.Equal("not-a-number", prepared.Character.Attributes["Stamina"]);
            Assert.Equal(map == 1039 ? 1000L : 950L, prepared.Character.MinMana);
            Assert.Equal(checked((uint)prepared.Character.MinMana), prepared.NativePlayer!.Mana);
            var frames = f.Frames(PlayerUid).Skip(first).ToArray();
            Assert.DoesNotContain(ParseMsgUpdates(frames), packet => packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
            Assert.Equal((root ? 0 : 1) + (map == 1039 ? 0 : 1), frames.Length);
            Assert.True(prepared.MagicCooldowns!.ContainsKey(root ? (ushort)1000 : (ushort)1045));
            Assert.False(f.Recovery.IsBlocked);
        }, cost);
}
