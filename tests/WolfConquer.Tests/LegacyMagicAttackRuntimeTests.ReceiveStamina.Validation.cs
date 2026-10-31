using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(true, "-1")] [InlineData(false, "-1")]
    [InlineData(true, "not-a-number")] [InlineData(false, "not-a-number")]
    [InlineData(true, "18446744073709551616")] [InlineData(false, "18446744073709551616")]
    [InlineData(true, "")] [InlineData(false, "")]
    public Task ReceiveStaminaCapriceInvalidSavedParticipantDoesNotPrepareEitherBalanceOrStamp(bool invalidActor, string raw) =>
        ReceiveStaminaRejectedSavedAsync(invalidActor ? raw : "40", invalidActor ? "80" : raw, caprice: true);

    [Theory]
    [InlineData("18446744073709551615")] [InlineData("18446744073709551596")]
    public Task ReceiveStaminaCapriceUnrepresentableGainRejectsBeforeTargetDebit(string raw) =>
        ReceiveStaminaRejectedSavedAsync(raw, "80", caprice: true);

    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")] [InlineData("18446744073709551616")] [InlineData("")]
    public Task ReceiveStaminaLightInvalidSavedBalanceDoesNotAcceptOrRestore(string raw) =>
        ReceiveStaminaRejectedSavedAsync(raw, "80", caprice: false);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ReceiveStaminaCapriceAbsenceIsNativeZeroAndCannotSpendEitherBalanceOrCooldown(bool blessing) =>
        ReceiveStaminaOwnedAsync(null, null, true, async (f, world) =>
        {
            var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); var files = ProcFiles(f);
            foreach (uint uid in new[] { PlayerUid, TargetPlayerUid })
            {
                Assert.False(f.Data.Loader.LoadByAccountId(uid)!.Attributes.ContainsKey("Stamina"));
                Assert.Equal("0", f.Current(uid).Character.Attributes["Stamina"]);
                Assert.Equal((ushort)0, f.Current(uid).NativePlayer!.Stamina);
            }
            int a = f.Frames(PlayerUid).Count, p = f.Frames(TargetPlayerUid).Count;
            var prepared = ReceiveStaminaPrepare(f, true); Assert.False(prepared.Applied); Assert.False(prepared.Restored);
            Assert.Same(actor.Character, prepared.Actor); Assert.Same(peer.Character, prepared.Peer);
            ReceiveStaminaUnchanged(f, actor, peer, files, a, p);
            string stamp = actor.Character.Attributes["LegacyCapriceStampUtc"];
            await ReceiveStaminaAttackAsync(f, world);
            foreach (uint uid in new[] { PlayerUid, TargetPlayerUid })
            {
                Assert.Equal("0", f.Current(uid).Character.Attributes["Stamina"]);
                Assert.Equal((ushort)0, f.Current(uid).NativePlayer!.Stamina);
                Assert.False(f.Data.Loader.LoadByAccountId(uid)!.Attributes.ContainsKey("Stamina"));
            }
            Assert.Equal(stamp, f.Current(PlayerUid).Character.Attributes["LegacyCapriceStampUtc"]);
            Assert.DoesNotContain(ParseMsgUpdates(f.Frames(PlayerUid).Concat(f.Frames(TargetPlayerUid)))
                .SelectMany(update => update.Updates), update => update.Type == LegacyMsgUpdateDataType.Stamina);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        }, blessing);

    [Fact]
    public Task ReceiveStaminaCapriceFormerUInt64PositiveCannotBecomeANativeOwner() =>
        ReceiveStaminaRejectedSavedAsync("18446744073709551595", "80", caprice: true);

    [Fact]
    public Task ReceiveStaminaIndependentXpBranchStillCannotAdmitMalformedSavedParticipants() =>
        ReceiveStaminaRejectedSavedAsync("not-a-number", "-1", caprice: true, branch: 2);

    [Fact]
    public Task ReceiveStaminaCapriceNativeMaximumGainCommitsBothNativeValuesBeforeLiteralReply() =>
        ReceiveStaminaOwnedAsync("65515", "80", true, async (f, world) =>
        {
            bool observed = false;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != PlayerUid || Convert.ToHexString(bytes) != "1000800808A0FA4E1206080810FFFF03") return;
                observed = true;
                Assert.Equal("65535", f.Current(PlayerUid).Character.Attributes["Stamina"]);
                Assert.Equal("60", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
                Assert.Equal(ushort.MaxValue, f.Current(PlayerUid).NativePlayer!.Stamina);
                Assert.Equal((ushort)60, f.Current(TargetPlayerUid).NativePlayer!.Stamina);
                foreach (uint participant in new[] { PlayerUid, TargetPlayerUid })
                    Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current(participant).NativeGameClient!.OrdinarySpellBinding!));
                Assert.Equal("65535", f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes["Stamina"]);
                Assert.Equal("60", f.Data.Loader.LoadByAccountId(TargetPlayerUid)!.Attributes["Stamina"]);
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            };
            try { await ReceiveStaminaAttackAsync(f, world); }
            finally { f.BeforeSend = null; }
            Assert.True(observed);
            Assert.Contains(f.Frames(PlayerUid), bytes => Convert.ToHexString(bytes) == "1000800808A0FA4E1206080810FFFF03");
            Assert.Contains(f.Frames(TargetPlayerUid), bytes => Convert.ToHexString(bytes) == ReceiveStaminaPeer60);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
        });

    [Theory]
    [InlineData("65516")] [InlineData("65535")]
    public Task ReceiveStaminaCapriceAdmittedNativeGainOverflowKeepsBalancesAndStampWhileOuterHitCommits(string raw) =>
        ReceiveStaminaOwnedAsync(raw, "80", true, async (f, world) =>
        {
            var originalStamp = f.Current(PlayerUid).Character.Attributes["LegacyCapriceStampUtc"];
            var native = ushort.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            int replies = 0;
            f.BeforeSend = (_, _) =>
            {
                replies++;
                Assert.Equal(raw, f.Current(PlayerUid).Character.Attributes["Stamina"]);
                Assert.Equal("80", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
                Assert.Equal(native, f.Current(PlayerUid).NativePlayer!.Stamina);
                Assert.Equal((ushort)80, f.Current(TargetPlayerUid).NativePlayer!.Stamina);
                Assert.Equal(raw, f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes["Stamina"]);
                Assert.Equal("80", f.Data.Loader.LoadByAccountId(TargetPlayerUid)!.Attributes["Stamina"]);
                Assert.Equal(originalStamp, f.Current(PlayerUid).Character.Attributes["LegacyCapriceStampUtc"]);
            };
            try { await ReceiveStaminaAttackAsync(f, world); }
            finally { f.BeforeSend = null; }
            Assert.True(replies > 0);
            Assert.Equal(originalStamp, f.Data.Loader.LoadByAccountId(PlayerUid)!.Attributes["LegacyCapriceStampUtc"]);
            Assert.DoesNotContain(ParseMsgUpdates(f.Frames(PlayerUid).Concat(f.Frames(TargetPlayerUid)))
                .SelectMany(update => update.Updates), update => update.Type == LegacyMsgUpdateDataType.Stamina);
            ReceiveStaminaOuterAttack(f); Assert.False(f.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
}
