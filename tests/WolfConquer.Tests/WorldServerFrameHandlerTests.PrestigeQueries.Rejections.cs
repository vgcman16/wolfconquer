using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    public static IEnumerable<object[]> PrestigeQueryCorruptions() =>
        PrestigeRankingQueryTestData.Corruptions.Select(kind => new object[] { kind });

    [Theory]
    [MemberData(nameof(PrestigeQueryCorruptions))]
    public Task OrdinaryRankMemberShowNeverPublishesAChampionBeforeValidatingCorruptLowScoreTail(string corruption) =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var owner = fixture.Current;
            var files = fixture.SavedFiles();
            // Declared catalog-input corruption after creation permits null and
            // malformed tails without a fabricated accepting catalog factory.
            fixture.SourceRows[1] = PrestigeRankingQueryTestData.Corruption(corruption)!;
            await fixture.Query();
            Assert.Empty(fixture.Sent);
            Assert.Same(owner, fixture.Current);
            fixture.AssertFilesUnchanged(files);
            Assert.Contains("required prestige catalog is invalid", fixture.Log.ToString());
        }, [PrestigeRankingQueryTestData.Row(), PrestigeRankingQueryTestData.Row(99, total: 1)]);

    [Fact]
    public Task OrdinaryRankMemberShowRejectsDuplicateUidInUnrankedOtherClassTailWithoutPartialFrame() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            fixture.SourceRows[1] = PrestigeRankingQueryTestData.Row(41, total: 1, category: 3, name: "Duplicate");
            await fixture.Query();
            Assert.Empty(fixture.Sent);
            Assert.Contains("required prestige catalog is invalid", fixture.Log.ToString());
        }, [PrestigeRankingQueryTestData.Row(), PrestigeRankingQueryTestData.Row(99, total: 1)]);

    [Fact]
    public Task OrdinaryRankMemberShowMissingRequiredCatalogIsSilentAndReadOnly() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var files = fixture.SavedFiles();
            await fixture.Query(context: fixture.Context with { WorldData = null });
            Assert.Empty(fixture.Sent);
            fixture.AssertFilesUnchanged(files);
            Assert.Contains("required prestige catalog is unavailable", fixture.Log.ToString());
        });

    [Fact]
    public Task OrdinaryRankMemberShowUnencodableChampionPublishesNoPartialFrame() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            fixture.SourceRows[0] = PrestigeRankingQueryTestData.Row(name: new string('N', 70_000));
            var files = fixture.SavedFiles();
            await fixture.Query();
            Assert.Empty(fixture.Sent);
            fixture.AssertFilesUnchanged(files);
            Assert.Contains("required prestige catalog cannot be encoded", fixture.Log.ToString());
        });

    [Fact]
    public Task OrdinaryRankMemberShowRefusesMissingSessionForeignConnectionAndDetachedSession() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var context = fixture.Context;
            await fixture.Query(context: context with { Session = null });
            Assert.Empty(fixture.Sent);
            var foreignFrames = new List<byte[]>();
            var foreign = fixture.CreateOwnedSendingConnection(foreignFrames);
            await fixture.Query(context: context with { Connection = foreign });
            Assert.Empty(fixture.Sent);
            Assert.Empty(foreignFrames);
            await fixture.Query(context: context with { Sessions = null });
            Assert.Empty(fixture.Sent);
            Assert.True(fixture.Sessions.TryRemove(fixture.Connection, out var removed));
            Assert.Same(context.Session, removed);
            await fixture.Query(context: context);
            Assert.Empty(fixture.Sent);
        });

    [Fact]
    public Task OrdinaryRankMemberShowMalformedTailAndCancellationCannotPublishAPrefix() =>
        RunPrestigeQueryAsync(async fixture =>
        {
            var valid = ChampionRequest();
            var malformed = new PacketWriter(WorldPacketOpcodes.LegacyRankMemberShow, valid.Length - 4 + 1);
            valid.AsSpan(4).CopyTo(malformed.Payload);
            malformed.Payload[^1] = 0x80; // Unterminated varint after a complete first item.
            await fixture.Query(malformed.ToArray());
            Assert.Empty(fixture.Sent);
            Assert.Contains("rejected malformed LegacyRankMemberShow", fixture.Log.ToString());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Query(cancellationToken: cancellation.Token));
            Assert.Empty(fixture.Sent);
        });
}
