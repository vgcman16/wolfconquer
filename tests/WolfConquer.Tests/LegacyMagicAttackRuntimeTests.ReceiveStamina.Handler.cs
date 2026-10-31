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
            bool committed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != PlayerUid || Convert.ToHexString(bytes) != ReceiveStaminaActor60) return;
                committed = true;
                foreach (uint participant in new[] { PlayerUid, TargetPlayerUid })
                {
                    Assert.Equal("60", f.Current(participant).Character.Attributes["Stamina"]);
                    Assert.Equal((ushort)60, f.Current(participant).NativePlayer!.Stamina);
                    Assert.Equal("60", f.Data.Loader.LoadByAccountId(participant)!.Attributes["Stamina"]);
                }
            };
            try { await ReceiveStaminaAttackAsync(f, world); }
            finally { f.BeforeSend = null; }
            Assert.True(committed);
            Assert.Equal("60", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal("60", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaActor60);
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaPeer60);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")] [InlineData("18446744073709551616")] [InlineData("")]
    public Task ReceiveStaminaInvalidSavedTargetCannotAcquireAReceiveParticipant(string raw) =>
        ReceiveStaminaRejectedSavedAsync("40", raw, caprice: true);

    [Fact]
    public Task ReceiveStaminaLightCannotUseTheOriginalMalformedSavedPeer() =>
        ReceiveStaminaRejectedSavedAsync("35", "not-a-number", caprice: false);

    [Fact]
    public Task ReceiveStaminaOwnedLightRestoresWithAValidPeerAndKeepsLiteralEffectAndStaminaWire() =>
        ReceiveStaminaOwnedAsync("35", "80", false, async (f, world) =>
        {
            await ReceiveStaminaAttackAsync(f, world);
            Assert.Equal("100", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal("80", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal((ushort)100, f.Current(PlayerUid).NativePlayer!.Stamina);
            Assert.Equal((ushort)80, f.Current(TargetPlayerUid).NativePlayer!.Stamina);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaActor100);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ReceiveStaminaLightRestoresActualAdmittedAbsenceZeroWithoutBlessingFallback(bool blessing) =>
        ReceiveStaminaOwnedAsync(null, "80", false, async (f, world) =>
        {
            Assert.False(f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes.ContainsKey("Stamina"));
            Assert.Equal("0", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal((ushort)0, f.Current(PlayerUid).NativePlayer!.Stamina);
            await ReceiveStaminaAttackAsync(f, world);
            Assert.Equal("100", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal((ushort)100, f.Current(PlayerUid).NativePlayer!.Stamina);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaActor100);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaLightEffect);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
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
