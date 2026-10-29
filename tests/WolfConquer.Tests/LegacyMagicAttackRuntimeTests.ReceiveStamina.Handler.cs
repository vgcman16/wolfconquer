using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Literal successful D13 bytes, separate from the producer/packet builders.
    private const string ReceiveStaminaActor60 = "0E00800808A0FA4E12040808103C";
    private const string ReceiveStaminaPeer60 = "0E00800808A1FA4E12040808103C";
    private const string ReceiveStaminaActor100 = "0E00800808A0FA4E120408081064";
    private const string ReceiveStaminaLightEffect = "0E000F0908A1FA4E10A1FA4E1807";

    [Fact]
    public Task ReceiveStaminaOwnedCapriceKeepsSuccessfulTransferWireAndOuterDamage() =>
        ReceiveStaminaOwnedAsync("40", "80", true, async (f, world) =>
        {
            await ReceiveStaminaAttackAsync(f, world);
            Assert.Equal("60", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal("60", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaActor60);
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaPeer60);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")] [InlineData("18446744073709551616")] [InlineData("")]
    public Task ReceiveStaminaOwnedInvalidTargetRejectsTransferButKeepsIndependentAcceptedAttack(string raw) =>
        ReceiveStaminaOwnedAsync("40", raw, true, async (f, world) =>
        {
            var originalStamp = f.Current(PlayerUid).Character.Attributes["LegacyCapriceStampUtc"];
            await ReceiveStaminaAttackAsync(f, world);
            Assert.Equal("40", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(TargetPlayerUid)!.Attributes["Stamina"]);
            Assert.Equal(originalStamp, f.Current(PlayerUid).Character.Attributes["LegacyCapriceStampUtc"]);
            Assert.DoesNotContain(ParseMsgUpdates(f.Frames(PlayerUid).Concat(f.Frames(TargetPlayerUid)))
                .SelectMany(update => update.Updates), update => update.Type == LegacyMsgUpdateDataType.Stamina);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Fact]
    public Task ReceiveStaminaOwnedLightRestoresAndKeepsLiteralEffectAndStaminaWire() =>
        ReceiveStaminaOwnedAsync("35", "not-a-number", false, async (f, world) =>
        {
            await ReceiveStaminaAttackAsync(f, world);
            Assert.Equal("100", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal("not-a-number", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaActor100);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ReceiveStaminaLightAbsentBalanceKeepsExistingDefaultWithoutInventingRestore(bool blessing) =>
        ReceiveStaminaOwnedAsync(null, "80", false, (f, _) =>
        {
            var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); var files = ProcFiles(f);
            int a = f.Frames(PlayerUid).Count, p = f.Frames(TargetPlayerUid).Count;
            var prepared = ReceiveStaminaPrepare(f, false);
            Assert.True(prepared.Applied); Assert.False(prepared.Restored); Assert.Same(actor.Character, prepared.Actor);
            ReceiveStaminaUnchanged(f, actor, peer, files, a, p); return Task.CompletedTask;
        }, blessing);

    private static void ReceiveStaminaOuterAttack(OwnedEquipmentBehaviorFixture f)
    {
        Assert.Equal(500 - 7, f.Current(PlayerUid).Character.MinMana);
        Assert.Equal(500 - 7, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, f.Current(TargetPlayerUid).Character.MinHitPoints);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, f.Data.Loader.LoadByAccountId(TargetPlayerUid)!.MinHitPoints);
        AssertNormalThunderDamage(f.Frames(PlayerUid)); AssertNormalThunderDamage(f.Frames(TargetPlayerUid));
    }
}
