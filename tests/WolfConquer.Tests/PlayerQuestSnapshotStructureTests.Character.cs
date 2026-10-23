using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class PlayerQuestSnapshotStructureTests
{
    [Theory]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public void RealQuestLoaderRejectsMalformedOwnedFileWithoutChangingAnyFile(string failure) => Run(files =>
    {
        files.Seed(BadImage(failure));
        var before = files.CaptureFiles();

        var error = Assert.Throws<FormatException>(
            () => new PlayerQuestSnapshotLoader(files.Quests).LoadByCharacterUid(CharacterUid));

        Assert.Contains(CharacterUid + ".bin", error.Message);
        files.AssertFilesEqual(before);
    });

    [Theory]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public void CharacterTryLoadClassifiesMalformedQuestBeforePaidNobilityPersistence(string failure) => Run(files =>
    {
        files.Seed(BadImage(failure), expiredPaidNobility: true);
        var before = files.CaptureFiles();

        CharacterSnapshotLoadResult? result = null;
        var error = Record.Exception(() => result = files.Loader.TryLoadByAccountId(CharacterUid));

        files.AssertFilesEqual(before); // Prove rejection has no earlier expiry write, even if the call throws.
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Equal(CharacterSnapshotLoadStatus.SnapshotParseFailed, result.Status);
        Assert.Equal("questSnapshotParseFailed", result.Reason);
        Assert.Equal(CharacterUid, result.AccountId);
        Assert.Equal(files.UserPath, result.Path);
        Assert.Contains(CharacterUid + ".bin", result.Detail);
        files.AssertFilesEqual(before);

        Assert.Throws<FormatException>(() => files.Loader.LoadByAccountId(CharacterUid));
        files.AssertFilesEqual(before);
    });

    [WindowsTradeJournalFailureFact]
    public void CharacterTryLoadClassifiesRealQuestReadFailureBeforePaidNobilityPersistence() => Run(files =>
    {
        files.Seed(ValidImage(), expiredPaidNobility: true);
        var before = files.CaptureFiles();
        CharacterSnapshotLoadResult? result = null;
        Exception? error;
        using (new FileStream(files.QuestPath, FileMode.Open, FileAccess.Read, FileShare.None))
            error = Record.Exception(() => result = files.Loader.TryLoadByAccountId(CharacterUid));

        files.AssertFilesEqual(before);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Equal(CharacterSnapshotLoadStatus.SnapshotReadFailed, result.Status);
        Assert.Equal("questSnapshotReadFailed", result.Reason);
        Assert.Equal(files.UserPath, result.Path);
        Assert.NotEmpty(result.Detail);
        files.AssertFilesEqual(before);
    });

    [Fact]
    public void UidMismatchStillPrecedesQuestParsingAndEveryExpiryWrite() => Run(files =>
    {
        files.Seed(OneHeader(int.MaxValue), expiredPaidNobility: true, uid: 999);
        var before = files.CaptureFiles();

        var result = files.Loader.TryLoadByAccountId(CharacterUid);

        Assert.Equal(CharacterSnapshotLoadStatus.UidMismatch, result.Status);
        Assert.Equal("uidMismatch", result.Reason);
        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Equal(files.UserPath, result.Path);
        files.AssertFilesEqual(before);
    });

    [Fact]
    public void HealthyQuestImageStillLoadsBeforeLegitimateExpiredNobilityRestoration() => Run(files =>
    {
        var questBytes = ValidImage();
        files.Seed(questBytes, expiredPaidNobility: true);
        var before = files.CaptureFiles();

        var result = files.Loader.TryLoadByAccountId(CharacterUid);

        Assert.True(result.Succeeded, result.Detail);
        Assert.Equal("success", result.Reason);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(questBytes, PlayerQuestSnapshotLoader.Serialize(result.Snapshot.PlayerQuests));
        Assert.Same(result.Snapshot.PlayerQuests.Quests[0], Assert.Single(result.Snapshot.PlayerQuests.AcceptedQuests));
        Assert.Equal("100000000", result.Snapshot.Attributes["DonationNobility"]);
        Assert.Equal("3", result.Snapshot.Attributes["NobilityRank"]);
        Assert.Equal("False", result.Snapshot.PaidNobility["IsActive"]);
        Assert.Equal(questBytes, File.ReadAllBytes(files.QuestPath));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), files.CaptureFiles().Keys.Order(StringComparer.Ordinal));
        var text = CharacterSnapshotFileEncoding.ReadAllText(files.UserPath, out _);
        Assert.Contains("Keep=unchanged-µ", text);
        var persisted = CharacterSnapshotParser.ParseText(text);
        Assert.Equal("100000000", persisted.Attributes["DonationNobility"]);
        Assert.Equal("False", persisted.PaidNobility["IsActive"]);
        Assert.Equal(998877, persisted.Money);
        Assert.Equal(1122, persisted.ConquerPoints);
    });

    [Fact]
    public void MissingOptionalQuestFileRetainsSharedEmptySnapshotAndNoNewFile() => Run(files =>
    {
        files.Seed(questImage: null);
        var before = files.CaptureFiles();

        var result = files.Loader.TryLoadByAccountId(CharacterUid);

        Assert.True(result.Succeeded, result.Detail);
        Assert.NotNull(result.Snapshot);
        Assert.Same(PlayerQuestSnapshot.Empty, result.Snapshot.PlayerQuests);
        files.AssertFilesEqual(before);
        Assert.False(File.Exists(files.QuestPath));
    });

    [Fact]
    public void AFailedReloadDoesNotMutateThePreviouslyReturnedQuestGraph() => Run(files =>
    {
        files.Seed(ValidImage());
        var previous = files.Loader.LoadByAccountId(CharacterUid);
        Assert.NotNull(previous);
        var previousQuestBytes = PlayerQuestSnapshotLoader.Serialize(previous.PlayerQuests);
        var accepted = Assert.Single(previous.PlayerQuests.AcceptedQuests);
        File.WriteAllBytes(files.QuestPath, OneHeader(int.MaxValue));
        var before = files.CaptureFiles();

        var result = files.Loader.TryLoadByAccountId(CharacterUid);

        Assert.Equal(CharacterSnapshotLoadStatus.SnapshotParseFailed, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Equal(previousQuestBytes, PlayerQuestSnapshotLoader.Serialize(previous.PlayerQuests));
        Assert.Same(accepted, Assert.Single(previous.PlayerQuests.AcceptedQuests));
        Assert.Same(previous.PlayerQuests.Quests[0], accepted);
        files.AssertFilesEqual(before);
    });

    private static byte[] BadImage(string failure) => failure switch
    {
        "negative" => OneHeader(-1),
        "overflow" => OneHeader(int.MaxValue),
        "truncated" => OneHeader(1),
        "trailing" => new byte[sizeof(int) + 1],
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };
}
