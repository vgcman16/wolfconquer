using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task RealChangePersistsBeforeGoldenSuccessAndPreservesUnrelatedStateAndEncoding(int kind) =>
        InFixture(async fixture =>
        {
            fixture.VerifyCommittedBeforeSend = true;
            await fixture.Dispatch();
            Assert.Equal(new byte[] { 0x0A, 0, 0x68, 8, 4, 3, 2, 1, 7, 3 }, Assert.Single(fixture.Sent));
            Assert.Equal("7", fixture.Current().Character.Attributes["MyTitle"]);
            Assert.Equal("2", fixture.Original.Character.Attributes["MyTitle"]);
            var saved = fixture.ReadPersisted();
            Assert.Equal("7", saved.Attributes["MyTitle"]);
            Assert.Equal("2,5,7", saved.Attributes["Titles"]);
            Assert.Equal("40010017", saved.Attributes["SpecialWingID"]);
            Assert.Equal("20010023", saved.Attributes["SpecialTitleID"]);
            Assert.Equal("73", saved.Attributes["SpecialTitleScore"]);
            Assert.Equal(12345, saved.Money);
            Assert.Equal("Crème€", saved.Name);
            var text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.ReadAllText(fixture.SnapshotPath, out var encoding);
            Assert.Equal(Fixture.EncodingFor(kind).CodePage, encoding.CodePage);
            Assert.Equal(Fixture.EncodingFor(kind).GetPreamble(), encoding.GetPreamble());
            Assert.Contains("SecurityPass=91357,0,0,", text);
            Assert.Contains("Sentinel=café€", text);
        }, kind);

    [Fact]
    public Task ListPreparesOrderedUniqueEnqueueGoldensWithoutChangingSaveOrSession() => InFixture(async fixture =>
    {
        var before = File.ReadAllBytes(fixture.SnapshotPath);
        await fixture.Dispatch(LegacyTitleAction.List);
        Assert.Equal(3, fixture.Sent.Count);
        for (int index = 0; index < 3; index++)
            Assert.Equal(new byte[] { 0x0A, 0, 0x68, 8, 4, 3, 2, 1, new byte[] { 2, 5, 7 }[index], 1 }, fixture.Sent[index]);
        Assert.Same(fixture.Original, fixture.Current());
        Assert.Equal(before, File.ReadAllBytes(fixture.SnapshotPath));
    }, titles: "2/5/5/0/7/");

    [Fact]
    public Task ClearZeroPersistsAndFollowingCurrentSessionCanSelectAgain() => InFixture(async fixture =>
    {
        await fixture.Dispatch(title: 0);
        Assert.Equal("0", fixture.ReadPersisted().Attributes["MyTitle"]);
        Assert.Equal("0", fixture.Current().Character.Attributes["MyTitle"]);
        Assert.Equal((byte)0, Assert.Single(fixture.Sent)[8]);
        var current = fixture.Current();
        await fixture.Dispatch(context: fixture.Context with { Session = current });
        Assert.Equal("7", fixture.ReadPersisted().Attributes["MyTitle"]);
        Assert.Equal(2, fixture.Sent.Count);
    });

    [Fact]
    public Task MissingSelectedFieldCanBePersistedAsExplicitCanonicalZero() => InFixture(async fixture =>
    {
        Assert.False(fixture.Original.Character.Attributes.ContainsKey("MyTitle"));
        await fixture.Dispatch(title: 0);
        var current = fixture.Current();
        Assert.Equal("0", current.Character.Attributes["MyTitle"]);
        Assert.Equal("0", fixture.ReadPersisted().Attributes["MyTitle"]);
        await fixture.Dispatch(title: 0, context: fixture.Context with { Session = current });
        Assert.Equal(2, fixture.Sent.Count);
    }, selected: null);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task PostCommitTransportFailureNeverRefundsOrReportsUncommittedRejection(int kind) => InFixture(async fixture =>
    {
        fixture.VerifyCommittedBeforeSend = true;
        fixture.SendFailure = kind switch
        {
            1 => new IOException("owned transport loss"),
            2 => new ObjectDisposedException("owned connection"),
            _ => new OperationCanceledException("owned late delivery cancellation"),
        };
        await fixture.Dispatch();
        Assert.Empty(fixture.Sent);
        Assert.Equal("7", fixture.Current().Character.Attributes["MyTitle"]);
        Assert.Equal("7", fixture.ReadPersisted().Attributes["MyTitle"]);
        Assert.Contains("outcome=committed delivery=stopped", fixture.Log.ToString());
        Assert.DoesNotContain("rejected without commit", fixture.Log.ToString());
        Assert.False(fixture.Recovery.IsBlocked);
    });
}
