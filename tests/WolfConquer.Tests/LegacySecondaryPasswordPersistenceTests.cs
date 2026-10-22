using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WindowsSecondaryPasswordPersistenceFactAttribute : FactAttribute
{
    public WindowsSecondaryPasswordPersistenceFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires Windows file-sharing replacement failures.";
    }
}

public sealed class LegacySecondaryPasswordPersistenceTests : IDisposable
{
    private const long Uid = 1_296_777;
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-secondary-password-" + Guid.NewGuid().ToString("N"));
    private readonly string baseText;

    public LegacySecondaryPasswordPersistenceTests()
    {
        Directory.CreateDirectory(UsersRoot);
        new CharacterSnapshotLoader(UsersRoot, null, null, null, null, null)
            .CreateLegacyFirstCharacterSnapshot(Uid, "Crème€", 1008, 10);
        baseText = File.ReadAllText(CharacterPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SuccessfulSecurityOnlyUpdateReloadsAndPreservesEncodingSectionsAndEscrow(int encodingKind)
    {
        var encoding = GetEncoding(encodingKind);
        var original = Seed("91357,0,0,", encoding);
        var updatedState = new LegacySecondaryPasswordState(24681, 1, new DateTime(2026, 10, 28, 13, 14, 15));

        Assert.True(LegacySecondaryPasswordPersistence.TryValidateBaseline(original, out var reason), reason);
        Assert.True(LegacySecondaryPasswordPersistence.TryPersist(original, updatedState, out reason), reason);

        var text = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out var actualEncoding);
        Assert.Equal(encoding.CodePage, actualEncoding.CodePage);
        Assert.Equal(encoding.GetPreamble(), actualEncoding.GetPreamble());
        var loaded = CharacterSnapshotParser.ParseText(text);
        Assert.True(LegacySecondaryPasswordState.TryRead(loaded.Attributes, out var state, out reason), reason);
        Assert.Equal(updatedState, state);
        Assert.Equal("24681,1," + updatedState.ResetAt.Ticks.ToString(CultureInfo.InvariantCulture) + ",", loaded.Attributes["SecurityPass"]);
        Assert.Equal(original.Character.Money, loaded.Money);
        Assert.Equal(original.Character.ConquerPoints, loaded.ConquerPoints);
        Assert.Equal("Crème€", loaded.Name);
        Assert.Contains("ManagedTradeEscrowId=4be4f12054804da1a71c737bbbcde226", text);
        Assert.Contains("ManagedTradeEscrowMoney=17", text);
        Assert.Contains("ManagedTradeEscrowCP=9", text);
        Assert.Contains("[Unrelated]", text);
        Assert.Contains("Sentinel=café€", text);
        Assert.Contains("SecurityPass=not-a-character-state", text);
        Assert.Contains("[PaidNobility]", text);
        Assert.Equal("91357,0,0,", original.Character.Attributes["SecurityPass"]);
        Assert.Empty(Directory.GetFiles(UsersRoot, "*.character-file-*"));
    }

    [Fact]
    public void WithStateClonesOnlyAttributesAndDoesNotChangeTheOriginalSession()
    {
        var original = Seed("91357,0,0,");
        var changed = LegacySecondaryPasswordPersistence.WithState(original, new(24681, 0, default));
        Assert.NotSame(original, changed);
        Assert.NotSame(original.Character.Attributes, changed.Character.Attributes);
        Assert.Same(original.PlayerItems, changed.PlayerItems);
        Assert.Equal(original.Character.Money, changed.Character.Money);
        Assert.Equal("91357,0,0,", original.Character.Attributes["SecurityPass"]);
        Assert.Equal("24681,0,0,", changed.Character.Attributes["SecurityPass"]);
        Assert.Equal(original.Character.Attributes["ManagedTradeEscrowMoney"], changed.Character.Attributes["ManagedTradeEscrowMoney"]);
    }

    [Fact]
    public void EquivalentParsedButDifferentRawSecurityFieldIsStillStale()
    {
        var original = Seed("91357,0,0,");
        File.WriteAllText(CharacterPath, File.ReadAllText(CharacterPath).Replace("91357,0,0,", "091357,0,0,", StringComparison.Ordinal));
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordStateStale");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingAndPresentZeroFieldsAreNotInterchangeableBaselines(bool expectedPresent, bool diskPresent)
    {
        var original = Seed(expectedPresent ? "0,0,0," : null);
        var text = File.ReadAllText(CharacterPath);
        text = diskPresent ? text.Replace("[Character]", "[Character]\r\nSecurityPass=0,0,0,")
            : text.Replace("SecurityPass=0,0,0,\r\n", string.Empty);
        File.WriteAllText(CharacterPath, text);
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordStateStale");
    }

    [Fact]
    public void BothMissingFieldsAllowFirstSecurityPasswordToBeStoredWithoutWalletRequirements()
    {
        var original = Seed(null);
        var text = string.Join("\r\n", File.ReadAllLines(CharacterPath).Where(line =>
            !line.StartsWith("Money=", StringComparison.Ordinal) && !line.StartsWith("ConquerPoints=", StringComparison.Ordinal)));
        File.WriteAllText(CharacterPath, text);
        Assert.True(LegacySecondaryPasswordPersistence.TryValidateBaseline(original, out var reason), reason);
        Assert.True(LegacySecondaryPasswordPersistence.TryPersist(original, new(24681, 0, default), out reason), reason);
        var actual = File.ReadAllText(CharacterPath);
        Assert.Contains("SecurityPass=24681,0,0,", actual);
        Assert.DoesNotContain(File.ReadAllLines(CharacterPath), line => line.StartsWith("Money=", StringComparison.Ordinal));
        Assert.DoesNotContain(File.ReadAllLines(CharacterPath), line => line.StartsWith("ConquerPoints=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("securitypass=91357,0,0,")]
    [InlineData("uid=1296777")]
    [InlineData("name=shadow")]
    [InlineData("[character]\r\nOther=value")]
    public void CaseDuplicateFieldsOrCharacterSectionsFailClosed(string extra)
    {
        var original = Seed("91357,0,0,");
        File.WriteAllText(CharacterPath, File.ReadAllText(CharacterPath).Replace("[Unrelated]", extra + "\r\n[Unrelated]"));
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordCharacter");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedExpectedOrDiskSecurityStateFailsClosedWithoutLeakingValues(bool corruptExpected)
    {
        var original = Seed("91357,0,0,");
        if (corruptExpected)
        {
            var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["SecurityPass"] = "91357,1,invalid-date" };
            original = original with { Character = original.Character with { Attributes = attrs } };
        }
        else File.WriteAllText(CharacterPath, File.ReadAllText(CharacterPath).Replace("91357,0,0,", "91357,1,invalid-date"));
        AssertRejectedWithoutChangingFile(original, corruptExpected ? "secondaryPasswordExpectedStateInvalid" : "secondaryPasswordDiskStateInvalid");
    }

    [Fact]
    public void WrongDiskUidIsNotAcceptedAsTheBoundAccount()
    {
        var original = Seed("91357,0,0,");
        File.WriteAllText(CharacterPath, File.ReadAllText(CharacterPath).Replace("UID=1296777", "UID=1296778"));
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordUidMismatch");
    }

    [Fact]
    public void CaseDuplicateExpectedSecurityAttributesCannotSelectOnePassword()
    {
        var original = Seed("91357,0,0,");
        var attrs = new Dictionary<string, string>(original.Character.Attributes, StringComparer.Ordinal)
        { ["securitypass"] = "24681,0,0," };
        original = original with { Character = original.Character with { Attributes = attrs } };
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordExpectedStateInvalid");
    }

    [Theory]
    [InlineData(0, Uid)]
    [InlineData(-1, Uid)]
    [InlineData(Uid, 0)]
    public void NonpositiveAccountOrCharacterBindingIsRejected(long accountId, long characterUid)
    {
        var original = Seed("91357,0,0,");
        original = original with { AccountId = accountId, Character = original.Character with { Uid = characterUid } };
        AssertRejectedWithoutChangingFile(original, "secondaryPasswordBindingInvalid");
    }

    [WindowsSecondaryPasswordPersistenceFact]
    public void LockedReplacementReturnsFailureOnlyAfterProvingExactOriginalBytesRemain()
    {
        var original = Seed("91357,0,0,", GetEncoding(2));
        var before = File.ReadAllBytes(CharacterPath);
        using (new FileStream(CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(LegacySecondaryPasswordPersistence.TryPersist(original, new(24681, 0, default), out var reason));
            Assert.StartsWith("secondaryPasswordWriteFailed:", reason);
            Assert.DoesNotContain("91357", reason);
            Assert.DoesNotContain("24681", reason);
            Assert.Equal(before, File.ReadAllBytes(CharacterPath));
        }
        Assert.True(LegacySecondaryPasswordPersistence.TryValidateBaseline(original, out var retryReason), retryReason);
        Assert.Empty(Directory.GetFiles(UsersRoot, "*.character-file-*"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string CharacterPath => Path.Combine(UsersRoot, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");

    private WorldSession Seed(string? securityText, Encoding? encoding = null)
    {
        var extra = (securityText is null ? "" : "SecurityPass=" + securityText + "\r\n") +
            "ManagedTradeEscrowId=4be4f12054804da1a71c737bbbcde226\r\nManagedTradeEscrowMoney=17\r\nManagedTradeEscrowCP=9\r\n" +
            "[Unrelated]\r\nSentinel=café€\r\nSecurityPass=not-a-character-state\r\n";
        var text = baseText.Replace("\r\n", "\n").Replace("\n", "\r\n")
            .Replace("[PaidNobility]", extra + "[PaidNobility]");
        File.WriteAllText(CharacterPath, text, encoding ?? new UTF8Encoding(false));
        return new(Uid, "synthetic-fixture", CharacterSnapshotParser.ParseText(text, Path.GetFileName(CharacterPath)),
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot);
    }

    private void AssertRejectedWithoutChangingFile(WorldSession original, string reasonPrefix)
    {
        var before = File.ReadAllBytes(CharacterPath);
        Assert.False(LegacySecondaryPasswordPersistence.TryValidateBaseline(original, out var reason));
        Assert.StartsWith(reasonPrefix, reason);
        Assert.False(LegacySecondaryPasswordPersistence.TryPersist(original, new(24681, 0, default), out reason));
        Assert.StartsWith(reasonPrefix, reason);
        Assert.DoesNotContain("91357", reason);
        Assert.DoesNotContain("24681", reason);
        Assert.DoesNotContain("invalid-date", reason);
        Assert.Equal(before, File.ReadAllBytes(CharacterPath));
    }

    private static Encoding GetEncoding(int kind)
    {
        if (kind != 2) return new UTF8Encoding(kind == 1);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
