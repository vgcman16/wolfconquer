using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real managed D13 handlers and disposable files with explicitly supplied native
// owner fixtures. This is not native transport-birth or actual-client UI proof.
public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Fact]
    public async Task VerificationUsesNewFieldAndExactAcknowledgmentsWithoutPersistingOrLoggingPins()
    {
        const uint uid = 1_297_001;
        const uint pin = 45123781;
        const uint wrong = 89231457;
        Seed(uid, $"{pin},0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(pin, player.NativeClient.Player.SecurityPassword);
        Assert.Contains(new LegacySecondaryPasswordPacket(5, 1, 0), PasswordReplies(player));
        var before = File.ReadAllBytes(UserPath(uid));

        AssertReply(await Password(owner, player, 1, oldPin: pin, newPin: wrong), 6, 0);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        AssertReply(await Password(owner, player, 1, oldPin: wrong, newPin: pin), 5, 257);
        Assert.True(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        Assert.DoesNotContain(pin.ToString(CultureInfo.InvariantCulture), owner.Log.ToString());
        Assert.DoesNotContain(wrong.ToString(CultureInfo.InvariantCulture), owner.Log.ToString());
    }

    [Fact]
    public async Task VerifiedProtectedOwnersTradeAndRetainVerificationAcrossLawfulRecordCopies()
    {
        const uint leftUid = 1_297_011;
        const uint rightUid = 1_297_012;
        Seed(leftUid, "111,0,0", 10);
        Seed(rightUid, "222,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        AssertReply(await Password(owner, left, 1, newPin: 111), 5, 257);
        AssertReply(await Password(owner, right, 1, newPin: 222), 5, 257);
        var previous = Current(owner, left);
        var attributes = new Dictionary<string, string>(previous.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["RuntimeTestCosmetic"] = "1",
        };
        owner.Sessions.Set(left.Connection, previous with { Character = previous.Character with { Attributes = attributes } });
        Assert.Same(left.NativeClient.Player, Current(owner, left).NativePlayer);
        Assert.True(left.NativeClient.Player.IsCheckedPass);

        await OpenTrade(owner, left, right);
        await Trade(owner, left, new(6) { ItemUID = 10 });
        await Trade(owner, right, new(6) { ItemUID = 20 });
        await Trade(owner, left, new(7) { Money = 300 });
        await Trade(owner, right, new(12) { CPS = 10 });
        await Trade(owner, left, new(9));
        await Trade(owner, right, new(9));

        Assert.False(Current(owner, left).LegacyTradeActive);
        Assert.False(Current(owner, right).LegacyTradeActive);
        Assert.Equal(20U, Assert.Single(Current(owner, left).PlayerItems.InventoryItems).Uid);
        Assert.Equal(10U, Assert.Single(Current(owner, right).PlayerItems.InventoryItems).Uid);
        var leftSaved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(leftUid)));
        var rightSaved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(rightUid)));
        Assert.Equal((700L, 60L), (leftSaved.Money, leftSaved.ConquerPoints));
        Assert.Equal((2_300L, 50L), (rightSaved.Money, rightSaved.ConquerPoints));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, left).PlayerItems), File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(owner, right).PlayerItems), File.ReadAllBytes(ItemPath(rightUid)));
        Assert.Equal((111U, 0U, 0L), ReadCredential(leftUid));
        Assert.Equal((222U, 0U, 0L), ReadCredential(rightUid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupNoticeAndForgedSnapshotCheckedFlagNeverAuthorizeProtectedTrade(bool forgeAttribute)
    {
        const uint leftUid = 1_297_021;
        const uint rightUid = 1_297_022;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        Assert.Contains(new LegacySecondaryPasswordPacket(5, 1, 0), PasswordReplies(left));
        if (forgeAttribute)
        {
            var current = Current(owner, left);
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["IsCheckedPass"] = "true",
            };
            owner.Sessions.Set(left.Connection, current with { Character = current.Character with { Attributes = attributes } });
        }
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        await AssertTradeDenied(owner, left, right);
        Assert.Equal((111U, 0U, 0L), ReadCredential(leftUid));
    }

    [Fact]
    public async Task FreshConnectionAndOwnerMustVerifyAgainEvenForTheSameCharacterUid()
    {
        const uint leftUid = 1_297_031;
        const uint rightUid = 1_297_032;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var oldOwner = NewOwner();
        var old = await Login(oldOwner, leftUid);
        AssertReply(await Password(oldOwner, old, 1, newPin: 111), 5, 257);
        Assert.True(old.NativeClient.Player.IsCheckedPass);

        var freshOwner = NewOwner();
        var fresh = await Login(freshOwner, leftUid);
        var right = await Login(freshOwner, rightUid);
        Assert.NotSame(old.NativeClient, fresh.NativeClient);
        Assert.NotSame(old.NativeClient.Player, fresh.NativeClient.Player);
        Assert.False(fresh.NativeClient.Player.IsCheckedPass);
        await AssertTradeDenied(freshOwner, fresh, right);
        AssertReply(await Password(freshOwner, fresh, 1, newPin: 111), 5, 257);
        await OpenTrade(freshOwner, fresh, right);
        await Trade(freshOwner, fresh, new(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementCurrentPlayerOrForeignCopiedSessionCannotInheritVerification(bool foreignCopy)
    {
        const uint leftUid = 1_297_041;
        const uint rightUid = 1_297_042;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        AssertReply(await Password(owner, left, 1, newPin: 111), 5, 257);
        Assert.True(left.NativeClient.Player.IsCheckedPass);
        Player attempted;
        if (foreignCopy)
        {
            attempted = CreateConnection(leftUid, left.NativeClient);
            owner.Sessions.Set(attempted.Connection, Current(owner, left) with { });
        }
        else
        {
            // Copying public scalar fields cannot forge the private issuer's
            // physical-player/current-connection initialization identity.
            left.NativeClient.Player = new LegacyNativePlayerRuntime(left.NativeClient)
            {
                UID = leftUid, SecurityPassword = 111, IsCheckedPass = true,
            };
            attempted = left;
        }
        var before = File.ReadAllBytes(UserPath(leftUid));
        await AssertTradeDenied(owner, attempted, right);
        AssertNoSuccess(await Password(owner, attempted, 1, newPin: 111));
        Assert.Equal(before, File.ReadAllBytes(UserPath(leftUid)));
        Assert.False(Current(owner, attempted).LegacyTradeActive);
    }

    [Theory]
    [InlineData(3U)]
    [InlineData(4U)]
    public async Task InitialSetPersistsWithExactAckAndServerOnlyActionsCannotChangeIt(uint action)
    {
        const uint uid = 1_297_051;
        Seed(uid, "0,0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        Assert.True(player.NativeClient.Player.IsCheckedPass);
        AssertReply(await Password(owner, player, action, oldPin: 999, newPin: 111), 5, 257);
        Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
        var saved = File.ReadAllBytes(UserPath(uid));

        foreach (var ignored in new uint[] { 3, 5, 6, 999 })
            Assert.Empty(await Password(owner, player, ignored, oldPin: 111, newPin: 222));
        Assert.Equal(saved, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
        Assert.True(player.NativeClient.Player.IsCheckedPass);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(222U)]
    public async Task ModifyRequiresOldFieldClearsResetPersistsAndDoesNotImplicitlyVerify(uint replacementPin)
    {
        const uint uid = 1_297_061;
        const uint rightUid = 1_297_062;
        var deadline = TestNow.DateTime.AddDays(3);
        Seed(uid, $"111,1,{deadline.Ticks}");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var right = await Login(owner, rightUid);
        var before = File.ReadAllBytes(UserPath(uid));
        AssertReply(await Password(owner, player, 4, oldPin: 999, newPin: 222), 6, 1);
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        AssertReply(await Password(owner, player, 4, oldPin: 111, newPin: replacementPin), 5, 257);
        Assert.Equal((replacementPin, 0U, deadline.Ticks), ReadCredential(uid));
        Assert.Equal(replacementPin, player.NativeClient.Player.SecurityPassword);
        Assert.Equal(0U, player.NativeClient.Player.OnReset);
        Assert.Equal(deadline.Ticks, player.NativeClient.Player.ResetSecurityPassowrd.Ticks);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        await AssertTradeDenied(owner, player, right);
        if (replacementPin == 0)
        {
            // Native action4 preserves the physical Player's checked flag, even
            // after removing its PIN. Only a fresh no-PIN login grants admission.
            Assert.Empty(await Password(owner, player, 1));
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            var freshOwner = NewOwner();
            var fresh = await Login(freshOwner, uid);
            var freshRight = await Login(freshOwner, rightUid);
            Assert.True(fresh.NativeClient.Player.IsCheckedPass);
            await OpenTrade(freshOwner, fresh, freshRight);
            await Trade(freshOwner, fresh, new(2));
        }
        else
        {
            AssertReply(await Password(owner, player, 1, newPin: 111), 6, 0);
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            AssertReply(await Password(owner, player, 1, newPin: replacementPin), 5, 257);
            Assert.True(player.NativeClient.Player.IsCheckedPass);
        }
    }

    [Fact]
    public async Task ForgetDateOverflowDiagnosticsRedactCredentialPayloadAndKeepAllStateUnchanged()
    {
        const uint uid = 1_297_065;
        const uint pin = 45123781;
        const uint ignoredNewPin = 89231457;
        Seed(uid, $"{pin},0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = File.ReadAllBytes(UserPath(uid));
        var current = Current(owner, player);
        owner.Clock.Now = DateTimeOffset.MaxValue;

        AssertNoSuccess(await Password(owner, player, 2, oldPin: pin, newPin: ignoredNewPin));

        Assert.Same(current, Current(owner, player));
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(pin, player.NativeClient.Player.SecurityPassword);
        Assert.Equal(0U, player.NativeClient.Player.OnReset);
        Assert.Equal(DateTime.MinValue, player.NativeClient.Player.ResetSecurityPassowrd);
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        var log = owner.Log.ToString();
        Assert.Contains("redacted", log, StringComparison.OrdinalIgnoreCase);
        foreach (var secret in new[] { pin, ignoredNewPin })
        {
            Assert.DoesNotContain(secret.ToString(CultureInfo.InvariantCulture), log);
            Assert.DoesNotContain(Convert.ToHexString(BitConverter.GetBytes(secret)), log, StringComparison.OrdinalIgnoreCase);
        }
        var frame = new LegacySecondaryPasswordPacket(2, pin, ignoredNewPin).ToFrameBytes();
        Assert.DoesNotContain(Convert.ToHexString(frame), log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(frame), log);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(1L)]
    public async Task ForgetSchedulesSevenLocalDaysAndOnlyStrictlyLaterLoadRemovesPassword(long offsetTicks)
    {
        const uint uid = 1_297_071;
        Seed(uid, "111,0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var deadline = TestNow.DateTime.AddDays(7);
        AssertReply(await Password(owner, player, 2), 5, 257);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(1U, player.NativeClient.Player.OnReset);
        Assert.Equal(deadline.Ticks, player.NativeClient.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal(DateTimeKind.Local, player.NativeClient.Player.ResetSecurityPassowrd.Kind);
        Assert.Equal((111U, 1U, deadline.Ticks), ReadCredential(uid));

        var freshOwner = NewOwner(TestNow.AddDays(7).AddTicks(offsetTicks));
        var fresh = await Login(freshOwner, uid);
        var expired = offsetTicks > 0;
        Assert.Equal(expired ? 0U : 111U, fresh.NativeClient.Player.SecurityPassword);
        Assert.Equal(expired ? 0U : 1U, fresh.NativeClient.Player.OnReset);
        Assert.Equal(expired, fresh.NativeClient.Player.IsCheckedPass);
        Assert.Equal(deadline.Ticks, fresh.NativeClient.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal((expired ? 0U : 111U, expired ? 0U : 1U, deadline.Ticks), ReadCredential(uid));
        if (expired) Assert.DoesNotContain(new LegacySecondaryPasswordPacket(5, 1, 0), PasswordReplies(fresh));
        else Assert.Contains(new LegacySecondaryPasswordPacket(5, 1, 0), PasswordReplies(fresh));
    }

    [Theory]
    [InlineData(1U)]
    [InlineData(2U)]
    [InlineData(4U)]
    public async Task LockedCredentialFileCannotAcknowledgeSuccessGrantVerificationOrMutateCredential(uint action)
    {
        const uint uid = 1_297_081;
        Seed(uid, "111,0,0");
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var before = File.ReadAllBytes(UserPath(uid));
        using (File.Open(UserPath(uid), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            AssertNoSuccess(await Password(owner, player, action, oldPin: 111, newPin: action == 1 ? 111U : 222U));
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            Assert.Equal(111U, player.NativeClient.Player.SecurityPassword);
            Assert.Equal(0U, player.NativeClient.Player.OnReset);
        }
        Assert.Equal(before, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal((111U, 0U, 0L), ReadCredential(uid));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("111,not-reset,0")]
    [InlineData("111,1,9223372036854775807")]
    public async Task MalformedSavedCredentialAllowsGameLoginButNeverPinVerificationOrTrade(string raw)
    {
        const uint leftUid = 1_297_091;
        const uint rightUid = 1_297_092;
        Seed(leftUid, raw);
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        var before = File.ReadAllBytes(UserPath(leftUid));
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        AssertNoSuccess(await Password(owner, left, 1, newPin: 111));
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        await AssertTradeDenied(owner, left, right);
        Assert.Equal(before, File.ReadAllBytes(UserPath(leftUid)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateCredentialOrUidFieldsRejectLoginWithoutMintingPinOrTradeAuthority(bool duplicateUid)
    {
        const uint leftUid = 1_297_101;
        const uint rightUid = 1_297_102;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        File.AppendAllText(UserPath(leftUid), duplicateUid ? $"UID={leftUid}\n" : "SecurityPass=0,0,0\n");
        var before = File.ReadAllBytes(UserPath(leftUid));
        var criticalBefore = CriticalCredentialLines(leftUid);
        var itemsBefore = File.ReadAllBytes(ItemPath(leftUid));
        var owner = NewOwner();
        var left = CreateConnection(leftUid);

        await owner.Handler.HandleFrameAsync(left.Connection,
            ReadFrame(new LegacyMsgLoginGamePacket(leftUid).ToFrameBytes()), left.Lifetime.Token);

        Assert.DoesNotContain(left.Sent, IsAnswerOk);
        Assert.False(owner.Sessions.TryGet(left.Connection, out _));
        Assert.Equal(0U, left.NativeClient.Player.UID);
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        Assert.Equal(before, File.ReadAllBytes(UserPath(leftUid)));
        Assert.Equal(criticalBefore, CriticalCredentialLines(leftUid));
        Assert.Contains("normalPrefixCharacterFieldsAmbiguous", owner.Log.ToString());

        var right = await Login(owner, rightUid);
        var acceptedRight = Current(owner, right);
        var rightBefore = File.ReadAllBytes(UserPath(rightUid));
        var rightItemsBefore = File.ReadAllBytes(ItemPath(rightUid));
        AssertNoSuccess(await Password(owner, left, 1, newPin: 111));
        await Trade(owner, left, new LegacyTradePacket(0) { UID = rightUid });

        Assert.False(owner.Sessions.TryGet(left.Connection, out _));
        Assert.Same(acceptedRight, Current(owner, right));
        Assert.False(Current(owner, right).LegacyTradeActive);
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        Assert.Equal(before, File.ReadAllBytes(UserPath(leftUid)));
        Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath(leftUid)));
        Assert.Equal(rightBefore, File.ReadAllBytes(UserPath(rightUid)));
        Assert.Equal(rightItemsBefore, File.ReadAllBytes(ItemPath(rightUid)));
        Assert.Equal(criticalBefore, CriticalCredentialLines(leftUid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForgingCurrentNativeCheckedFlagOrSnapshotZeroPinCannotReplaceVerificationIssuer(bool forgeNativeFlag)
    {
        const uint leftUid = 1_297_111;
        const uint rightUid = 1_297_112;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        var before = File.ReadAllBytes(UserPath(leftUid));
        if (forgeNativeFlag) left.NativeClient.Player.IsCheckedPass = true;
        else
        {
            var current = Current(owner, left);
            var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["SecurityPass"] = "0,0,0",
            };
            owner.Sessions.Set(left.Connection, current with { Character = current.Character with { Attributes = attributes } });
        }

        await AssertTradeDenied(owner, left, right);
        AssertNoSuccess(await Password(owner, left, 1, newPin: 111));

        Assert.Equal(before, File.ReadAllBytes(UserPath(leftUid)));
        Assert.Equal((111U, 0U, 0L), ReadCredential(leftUid));
        Assert.False(Current(owner, left).LegacyTradeActive);
        Assert.False(Current(owner, right).LegacyTradeActive);
    }

    [Fact]
    public async Task MissingLegacyCredentialInitializesPhysicalNoPinOwnerAndAllowsTradeAndInitialSet()
    {
        const uint leftUid = 1_297_121;
        const uint rightUid = 1_297_122;
        Seed(leftUid, "0,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        File.WriteAllLines(UserPath(leftUid), File.ReadAllLines(UserPath(leftUid)).Where(line =>
            !line.StartsWith("SecurityPass=", StringComparison.OrdinalIgnoreCase)));
        var owner = NewOwner();
        var left = await Login(owner, leftUid);
        var right = await Login(owner, rightUid);
        Assert.False(Current(owner, left).Character.Attributes.ContainsKey("SecurityPass"));
        Assert.True(left.NativeClient.Player.IsCheckedPass);
        Assert.Equal(0U, left.NativeClient.Player.SecurityPassword);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(left.Connection, Current(owner, left), out var state, out var isChecked));
        Assert.Equal(0U, state.Password);
        Assert.True(isChecked);

        await OpenTrade(owner, left, right);
        await Trade(owner, left, new(2));
        AssertReply(await Password(owner, left, 3, newPin: 111), 5, 257);

        Assert.Equal((111U, 0U, 0L), ReadCredential(leftUid));
        Assert.True(left.NativeClient.Player.IsCheckedPass);
    }

    [Fact]
    public async Task NonexpiredStartupDiskMismatchCannotInitializeOrInheritVerification()
    {
        const uint leftUid = 1_297_131;
        const uint rightUid = 1_297_132;
        Seed(leftUid, "111,0,0");
        Seed(rightUid, "0,0,0", 20, 2_000, 60);
        // Prime legitimate equipment/startup scalar normalization before injecting
        // the race. The exact-byte invariant below must not mistake the normal
        // HP clamp for a credential action's mutation, nor exclude any bytes.
        await Login(NewOwner(), leftUid);
        var owner = NewOwner();
        var native = new LegacyNativeGameClientRuntime();
        native.Player.IsCheckedPass = true; // Deliberate inherited-field attack before login.
        byte[]? racedBytes = null;
        var injected = false;
        var left = await Login(owner, leftUid, native, bytes =>
        {
            if (!IsAnswerOk(bytes) || injected) return;
            injected = true;
            // Source order: snapshot load and the local Online write precede
            // ANSWER_OK; security initialization follows it in StartWorldSession.
            // This is an injected external-writer race on disposable saves.
            var prior = File.ReadAllText(UserPath(leftUid));
            var changed = prior.Replace("SecurityPass=111,0,0", "SecurityPass=222,0,0", StringComparison.Ordinal);
            Assert.NotEqual(prior, changed);
            File.WriteAllText(UserPath(leftUid), changed);
            racedBytes = File.ReadAllBytes(UserPath(leftUid));
        });
        var right = await Login(owner, rightUid);
        Assert.True(injected);
        Assert.NotNull(racedBytes);
        Assert.Equal("111,0,0", Current(owner, left).Character.Attributes["SecurityPass"]);
        Assert.Equal((222U, 0U, 0L), ReadCredential(leftUid));
        Assert.False(left.NativeClient.Player.IsCheckedPass);
        // A protected=false binding would hide this startup bug until the next
        // baseline read. No private verification issuer may be minted at all.
        Assert.False(LegacySecondaryPasswordRuntime.TryGet(left.Connection, Current(owner, left), out _, out _));

        AssertNoSuccess(await Password(owner, left, 1, newPin: 111));
        AssertNoSuccess(await Password(owner, left, 1, newPin: 222));
        await AssertTradeDenied(owner, left, right);

        Assert.False(left.NativeClient.Player.IsCheckedPass);
        Assert.Equal(racedBytes, File.ReadAllBytes(UserPath(leftUid)));
        Assert.Equal((222U, 0U, 0L), ReadCredential(leftUid));
    }

    private string[] CriticalCredentialLines(uint uid) => File.ReadAllLines(UserPath(uid)).Where(line =>
        line.StartsWith("SecurityPass=", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("UID=", StringComparison.OrdinalIgnoreCase)).ToArray();
}
