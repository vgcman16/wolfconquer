using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real disposable character files and atomic replacements; not process-crash/power-loss proof.
public sealed class LegacyTradeEscrowClaimsTests : IDisposable
{
    private static readonly Guid TradeId = Guid.ParseExact("abcdef0123456789abcdef0123456789", "N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-claims-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AbsentAndCanonicalClearedFieldsHaveNoClaim()
    {
        Assert.True(LegacyTradeEscrowClaims.TryRead(new Dictionary<string, string>(), out var absent, out var reason), reason);
        Assert.Null(absent);
        Assert.True(LegacyTradeEscrowClaims.TryRead(LegacyTradeEscrowClaims.ClearValues, out var cleared, out reason), reason);
        Assert.Null(cleared);
    }

    [Theory]
    [InlineData("", "1", "0")]
    [InlineData("00000000000000000000000000000000", "1", "0")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789", "1", "0")]
    [InlineData("abcdef01-2345-6789-abcd-ef0123456789", "1", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "0", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "-1", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "01", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "+1", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", " 1", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "9223372036854775808", "0")]
    [InlineData("abcdef0123456789abcdef0123456789", "1", "4294967296")]
    public void NoncanonicalClaimsAreRejected(string id, string money, string cp)
    {
        Assert.False(LegacyTradeEscrowClaims.TryRead(ClaimFields(id, money, cp), out var claim, out var reason));
        Assert.Null(claim);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void PartialAndCaseDuplicateClaimFieldsAreRejected()
    {
        var partial = new Dictionary<string, string> { [LegacyTradeEscrowClaims.IdKey] = TradeId.ToString("N") };
        Assert.False(LegacyTradeEscrowClaims.TryRead(partial, out _, out _));
        var duplicate = new Dictionary<string, string>(ClaimFields(TradeId.ToString("N"), "1", "0"), StringComparer.Ordinal)
        { ["managedtradeescrowmoney"] = "1" };
        Assert.False(LegacyTradeEscrowClaims.TryRead(duplicate, out _, out _));
    }

    [Fact]
    public void WithClaimClonesAttributesAndPreservesUnrelatedValues()
    {
        var original = WriteSession();
        var updated = LegacyTradeEscrowClaims.WithClaim(original, new(TradeId, 100, 2));
        Assert.NotSame(original.Character.Attributes, updated.Character.Attributes);
        Assert.False(original.Character.Attributes.ContainsKey(LegacyTradeEscrowClaims.IdKey));
        Assert.Equal("preserved", updated.Character.Attributes["Sentinel"]);
        Assert.True(LegacyTradeEscrowClaims.TryRead(updated.Character.Attributes, out var claim, out var reason), reason);
        Assert.Equal(new LegacyTradeEscrowClaim(TradeId, 100, 2), claim);
        var cleared = LegacyTradeEscrowClaims.WithClaim(updated, null);
        Assert.True(LegacyTradeEscrowClaims.TryRead(cleared.Character.Attributes, out claim, out reason), reason);
        Assert.Null(claim);
        Assert.Throws<ArgumentException>(() => LegacyTradeEscrowClaims.WithClaim(original, new(Guid.Empty, 1, 0)));
    }

    [Fact]
    public void AtomicDebitWritesWalletAndClaimAndRestartRefundsExactlyOnce()
    {
        var original = WriteSession();
        var debited = Debit(original, 200, 5);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, debited, out var reason), reason);
        AssertDisk(800, 95, new(TradeId, 200, 5));
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out reason), reason);
        Assert.True(recovered);
        AssertDisk(1_000, 100, null);
        var refundedBytes = File.ReadAllBytes(UserPath());
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out recovered, out reason), reason);
        Assert.False(recovered);
        Assert.Equal(refundedBytes, File.ReadAllBytes(UserPath()));
        AssertNoArtifacts();
    }

    [Fact]
    public void AdditionalOffersAccumulateOnlyTheMatchingDebit()
    {
        var original = WriteSession();
        var first = Debit(original, 200, 5);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, first, out var reason), reason);
        var second = Debit(first, 100, 2, oldMoneyClaim: 200, oldCpClaim: 5);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(first, second, out reason), reason);
        AssertDisk(700, 93, new(TradeId, 300, 7));
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out reason), reason);
        Assert.True(recovered);
        AssertDisk(1_000, 100, null);
    }

    [Theory]
    [InlineData(900, 100, 101, 0)]
    [InlineData(900, 100, 99, 0)]
    [InlineData(1_001, 100, 1, 0)]
    [InlineData(1_000, 101, 0, 1)]
    [InlineData(1_000, 99, 0, 2)]
    public void ForgedCreditOrClaimNotMatchingDebitCannotWrite(long money, long cp, long claimMoney, uint claimCp)
    {
        var original = WriteSession();
        var before = File.ReadAllBytes(UserPath());
        var updated = LegacyTradeEscrowClaims.WithClaim(WithWallet(original, money, cp), new(TradeId, claimMoney, claimCp));
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(original, updated, out _));
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Fact]
    public void ExistingClaimCannotChangeTradeIdentityShrinkOrClearThroughDebitApi()
    {
        var original = WriteSession();
        var first = Debit(original, 200, 5);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, first, out var reason), reason);
        var before = File.ReadAllBytes(UserPath());
        var changedId = LegacyTradeEscrowClaims.WithClaim(WithWallet(first, 700, 95), new(Guid.NewGuid(), 300, 5));
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(first, changedId, out _));
        var shrunk = LegacyTradeEscrowClaims.WithClaim(first, new(TradeId, 100, 5));
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(first, shrunk, out _));
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(first, LegacyTradeEscrowClaims.WithClaim(first, null), out _));
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Fact]
    public void StaleWalletAndStaleClaimAreBothRejectedWithoutRewrite()
    {
        var original = WriteSession();
        Replace("Money=1000", "Money=999");
        var staleMoneyBytes = File.ReadAllBytes(UserPath());
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(original, Debit(original, 200, 5), out _));
        Assert.Equal(staleMoneyBytes, File.ReadAllBytes(UserPath()));
        original = WriteSession();
        var first = Debit(original, 200, 5);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, first, out var reason), reason);
        Replace(LegacyTradeEscrowClaims.MoneyKey + "=200", LegacyTradeEscrowClaims.MoneyKey + "=201");
        var staleClaimBytes = File.ReadAllBytes(UserPath());
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(first, Debit(first, 100, 0, 200, 5), out _));
        Assert.False(LegacyTradeEscrowClaims.TryValidateBaseline(UserPath(), first, out _));
        Assert.Equal(staleClaimBytes, File.ReadAllBytes(UserPath()));
    }

    [Fact]
    public void FailedAtomicReplacementLeavesBothWalletAndClaimAbsent()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows destination-sharing semantics.
        var original = WriteSession();
        var before = File.ReadAllBytes(UserPath());
        using (new FileStream(UserPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(original, Debit(original, 200, 5), out _));
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
        AssertNoArtifacts();
    }

    [Theory]
    [InlineData("ManagedTradeEscrowId=abcdef0123456789abcdef0123456789")]
    [InlineData("ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=-1\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=9223372036854775808\nManagedTradeEscrowCP=0")]
    [InlineData("ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=1\nManagedTradeEscrowCP=4294967296")]
    [InlineData("ManagedTradeEscrowId=abcdef0123456789abcdef0123456789\nManagedTradeEscrowMoney=1\nmanagedtradeescrowmoney=1\nManagedTradeEscrowCP=0")]
    [InlineData("Money=1000")]
    [InlineData("UID=1293600")]
    [InlineData("[Character]\nMoney=1000")]
    public void MalformedOrAmbiguousDiskStateIsNotRewritten(string extraCharacterFields)
    {
        if (!extraCharacterFields.Contains("ManagedTradeEscrow", StringComparison.OrdinalIgnoreCase))
            extraCharacterFields += "\n" + ClaimText(new(TradeId, 1, 0));
        WriteSession(extraCharacterFields: extraCharacterFields);
        var before = File.ReadAllBytes(UserPath());
        Assert.False(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out _));
        Assert.False(recovered);
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Fact]
    public void MalformedCharacterLineIsNotSilentlyIgnoredDuringRecovery()
    {
        WriteSession();
        Replace("[Unrelated]", "ManagedTradeEscrowMoney\n[Unrelated]");
        var before = File.ReadAllBytes(UserPath());
        Assert.False(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out _));
        Assert.False(recovered);
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Theory]
    [InlineData(long.MaxValue, 0, 1, 0)]
    [InlineData(0, uint.MaxValue, 0, 1)]
    public void RefundOverflowLeavesClaimAndWalletUntouched(long money, long cp, long claimMoney, uint claimCp)
    {
        WriteSession(money: money, cp: cp, extraCharacterFields: ClaimText(new(TradeId, claimMoney, claimCp)));
        var before = File.ReadAllBytes(UserPath());
        Assert.False(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out var reason));
        Assert.False(recovered);
        Assert.Equal("tradeEscrowRecoveryOverflow", reason);
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Fact]
    public void RefundAddsToCurrentDiskWalletRatherThanRestoringAnOldSnapshot()
    {
        var original = WriteSession();
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, Debit(original, 200, 5), out var reason), reason);
        Replace("Money=800", "Money=1100");
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out var recovered, out reason), reason);
        Assert.True(recovered);
        AssertDisk(1_300, 100, null);
    }

    [Fact]
    public void RecoveryPreflightsAllNumericFilesBeforeAnyRefundAndDoesNotRecurse()
    {
        WriteSession(uid: 1, extraCharacterFields: ClaimText(new(TradeId, 10, 1)));
        WriteSession(uid: 2, extraCharacterFields: LegacyTradeEscrowClaims.IdKey + "=broken");
        var first = File.ReadAllBytes(UserPath(1));
        var second = File.ReadAllBytes(UserPath(2));
        Assert.False(LegacyTradeEscrowClaims.TryRecoverAll(root, out var recovered, out _));
        Assert.Equal(0, recovered);
        Assert.Equal(first, File.ReadAllBytes(UserPath(1)));
        Assert.Equal(second, File.ReadAllBytes(UserPath(2)));
        File.Delete(UserPath(2));
        File.WriteAllText(Path.Combine(root, "notes.ini"), "not a character");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        File.WriteAllText(Path.Combine(root, "nested", "3.ini"), "not a character");
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out recovered, out var reason), reason);
        Assert.Equal(1, recovered);
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out recovered, out reason), reason);
        Assert.Equal(0, recovered);
    }

    [Fact]
    public void MissingUsersRootIsFreshServerNoOpButEmptyPathIsInvalid()
    {
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out var recovered, out var reason), reason);
        Assert.Equal(0, recovered);
        Assert.False(LegacyTradeEscrowClaims.TryRecoverAll("", out _, out _));
    }

    [Theory]
    [InlineData("[Character]\nUID=1\nName=Old\n")]
    [InlineData("[Character]\nUID=1\nMoney=1\n")]
    [InlineData("[Character]\nUID=1\nConquerPoints=1\n")]
    [InlineData("[Character]\nUID=1\nMoney=invalid\nConquerPoints=invalid\n")]
    [InlineData("[Legacy]\nNote=not-a-character\n")]
    public void RecoveryLeavesNoClaimLegacyFilesUntouchedWithoutNewWalletRequirements(string legacyText)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(UserPath(1), legacyText);
        var before = File.ReadAllBytes(UserPath(1));
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(1), out var recovered, out var reason), reason);
        Assert.False(recovered);
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out var count, out reason), reason);
        Assert.Equal(0, count);
        Assert.Equal(before, File.ReadAllBytes(UserPath(1)));
    }

    [Fact]
    public void NoClaimCompatibilityDoesNotWeakenActualTradeBaselineOrDebitValidation()
    {
        var original = WriteSession();
        Replace("Money=1000\n", string.Empty);
        var before = File.ReadAllBytes(UserPath());
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out var count, out var reason), reason);
        Assert.Equal(0, count);
        Assert.False(LegacyTradeEscrowClaims.TryValidateBaseline(UserPath(), original, out _));
        Assert.False(LegacyTradeEscrowClaims.TryPersistWallet(original, Debit(original, 1, 0), out _));
        Assert.Equal(before, File.ReadAllBytes(UserPath()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClaimedFileMissingWalletStillPreventsEveryRootRefund(bool clearedMetadata)
    {
        WriteSession(uid: 1, extraCharacterFields: ClaimText(new(TradeId, 10, 1)));
        var metadata = clearedMetadata
            ? string.Join('\n', LegacyTradeEscrowClaims.ClearValues.Select(pair => pair.Key + "=" + pair.Value))
            : ClaimText(new(TradeId, 10, 1));
        WriteSession(uid: 2, extraCharacterFields: metadata);
        File.WriteAllText(UserPath(2), File.ReadAllText(UserPath(2)).Replace("Money=1000\n", string.Empty, StringComparison.Ordinal));
        var first = File.ReadAllBytes(UserPath(1));
        var second = File.ReadAllBytes(UserPath(2));
        Assert.False(LegacyTradeEscrowClaims.TryRecoverAll(root, out var count, out _));
        Assert.Equal(0, count);
        Assert.Equal(first, File.ReadAllBytes(UserPath(1)));
        Assert.Equal(second, File.ReadAllBytes(UserPath(2)));
    }

    [Fact]
    public void NoClaimLegacyRecordDoesNotPreventAnotherCharactersValidRefund()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(UserPath(1), "[Character]\nUID=1\nName=Legacy\n");
        var legacy = File.ReadAllBytes(UserPath(1));
        WriteSession(uid: 2, extraCharacterFields: ClaimText(new(TradeId, 10, 1)));
        Assert.True(LegacyTradeEscrowClaims.TryRecoverAll(root, out var count, out var reason), reason);
        Assert.Equal(1, count);
        Assert.Equal(legacy, File.ReadAllBytes(UserPath(1)));
        var refunded = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(2)));
        Assert.Equal(1_010, refunded.Money);
        Assert.Equal(101, refunded.ConquerPoints);
        Assert.True(LegacyTradeEscrowClaims.TryRead(refunded.Attributes, out var claim, out reason), reason);
        Assert.Null(claim);
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("windows1252")]
    public void DebitAndRecoveryPreserveEncodingAndUnrelatedSections(string encodingName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding = encodingName switch
        {
            "utf8-bom" => new UTF8Encoding(true, true),
            "windows1252" => Encoding.GetEncoding(1252),
            _ => new UTF8Encoding(false, true),
        };
        var original = WriteSession(encoding: encoding);
        Assert.True(LegacyTradeEscrowClaims.TryPersistWallet(original, Debit(original, 100, 2), out var reason), reason);
        Assert.True(LegacyTradeEscrowClaims.TryRecoverCharacter(UserPath(), out _, out reason), reason);
        var bytes = File.ReadAllBytes(UserPath());
        Assert.Equal(encodingName == "utf8-bom", bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        var text = encoding.GetString(bytes);
        Assert.Contains("Name=Andreé", text, StringComparison.Ordinal);
        Assert.Contains("[Unrelated]", text, StringComparison.Ordinal);
        Assert.Contains("Money=do-not-touch", text, StringComparison.Ordinal);
        Assert.Contains("Note=preserve café", text, StringComparison.Ordinal);
        if (encodingName == "windows1252")
            Assert.Throws<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString(bytes));
        AssertNoArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UserPath(long uid = 1_293_600) => Path.Combine(root, uid.ToString(CultureInfo.InvariantCulture) + ".ini");

    private WorldSession WriteSession(long uid = 1_293_600, long money = 1_000, long cp = 100,
        string extraCharacterFields = "", Encoding? encoding = null)
    {
        Directory.CreateDirectory(root);
        var defaults = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
        var text = "[Character]\nUID=" + uid.ToString(CultureInfo.InvariantCulture) + "\nName=Andreé\n" +
            string.Join('\n', defaults.Select(key => key + "=0")) + "\nMoney=" + money.ToString(CultureInfo.InvariantCulture) +
            "\nConquerPoints=" + cp.ToString(CultureInfo.InvariantCulture) + "\nSentinel=preserved\n" + extraCharacterFields +
            "\n[Unrelated]\nMoney=do-not-touch\nNote=preserve café\n";
        File.WriteAllText(UserPath(uid), text, encoding ?? new UTF8Encoding(false, true));
        return new(uid, "fixture", CharacterSnapshotParser.ParseText(text, UserPath(uid)), PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: root);
    }

    private static WorldSession WithWallet(WorldSession session, long money, long cp) => session with
    {
        Character = session.Character with { Money = money, ConquerPoints = cp },
    };

    private static WorldSession Debit(WorldSession original, long money, uint cp,
        long oldMoneyClaim = 0, uint oldCpClaim = 0) => LegacyTradeEscrowClaims.WithClaim(
        WithWallet(original, original.Character.Money - money, original.Character.ConquerPoints - cp),
        new(TradeId, oldMoneyClaim + money, oldCpClaim + cp));

    private static Dictionary<string, string> ClaimFields(string id, string money, string cp) => new()
    {
        [LegacyTradeEscrowClaims.IdKey] = id,
        [LegacyTradeEscrowClaims.MoneyKey] = money,
        [LegacyTradeEscrowClaims.CpKey] = cp,
    };

    private static string ClaimText(LegacyTradeEscrowClaim claim) =>
        LegacyTradeEscrowClaims.IdKey + "=" + claim.TradeId.ToString("N") + "\n" +
        LegacyTradeEscrowClaims.MoneyKey + "=" + claim.Money.ToString(CultureInfo.InvariantCulture) + "\n" +
        LegacyTradeEscrowClaims.CpKey + "=" + claim.ConquerPoints.ToString(CultureInfo.InvariantCulture);

    private void AssertDisk(long money, long cp, LegacyTradeEscrowClaim? expectedClaim)
    {
        var character = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.ReadAllText(UserPath(), out _));
        Assert.Equal(money, character.Money);
        Assert.Equal(cp, character.ConquerPoints);
        Assert.True(LegacyTradeEscrowClaims.TryRead(character.Attributes, out var actualClaim, out var reason), reason);
        Assert.Equal(expectedClaim, actualClaim);
    }

    private void Replace(string oldValue, string newValue) => File.WriteAllText(UserPath(),
        File.ReadAllText(UserPath()).Replace(oldValue, newValue, StringComparison.Ordinal));

    private void AssertNoArtifacts() => Assert.Empty(Directory.EnumerateFiles(root, "*.character-file-*"));
}
