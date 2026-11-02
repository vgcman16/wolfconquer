using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal enum LegacyRelicDecomposeMigrationCase
{
    Ordinary, MissingSurplus, MissingDeclared, DuplicateOne, DuplicateTwo,
    NoMetadata, Full, Zero, Fifty, Maximum, EmptyWithCatalog,
}

public sealed partial class LegacyOwnedRelicDecomposeTests
{
    internal static Task RunMigrationCaseAsync(LegacyRelicDecomposeMigrationCase scenario)
    {
        var input = scenario switch
        {
            LegacyRelicDecomposeMigrationCase.NoMetadata => "noMetadata",
            LegacyRelicDecomposeMigrationCase.Full => "full",
            LegacyRelicDecomposeMigrationCase.DuplicateTwo => "stacked",
            _ => "valid",
        };
        return Run(async f =>
        {
            Assert.Equal(1, Ranking(f).FindRank(DecomposeFixture.Uid));
            Assert.Equal(2, Ranking(f).FindRank(DecomposeFixture.PeerUid));
            var peerFiles = File.ReadAllBytes(f.PeerItemsPath); var peer = f.PeerCurrent;
            var bytes = scenario switch
            {
                LegacyRelicDecomposeMigrationCase.MissingSurplus => DecomposeFixture.RequestWithCount(1, DecomposeFixture.First, uint.MaxValue),
                LegacyRelicDecomposeMigrationCase.MissingDeclared => DecomposeFixture.RequestWithCount(2, DecomposeFixture.First, uint.MaxValue),
                LegacyRelicDecomposeMigrationCase.DuplicateOne or LegacyRelicDecomposeMigrationCase.DuplicateTwo =>
                    DecomposeFixture.Request(DecomposeFixture.First, DecomposeFixture.First),
                LegacyRelicDecomposeMigrationCase.Zero => DecomposeFixture.RequestWithCount(0, DecomposeFixture.First),
                LegacyRelicDecomposeMigrationCase.EmptyWithCatalog => DecomposeFixture.RequestWithCount(0),
                LegacyRelicDecomposeMigrationCase.Fifty => DecomposeFixture.RequestWithCount(50, DecomposeFixture.First),
                LegacyRelicDecomposeMigrationCase.Maximum => DecomposeFixture.RequestWithCount(uint.MaxValue, DecomposeFixture.First),
                _ => DecomposeFixture.Request(DecomposeFixture.First),
            };
            bool success = scenario is LegacyRelicDecomposeMigrationCase.Ordinary or LegacyRelicDecomposeMigrationCase.MissingSurplus;
            if (success) await AssertSuccessfulDecompose(f, bytes, bound: false); else await AssertRefusal(f, bytes);
            Assert.Equal(peerFiles, File.ReadAllBytes(f.PeerItemsPath)); Assert.Same(peer, f.PeerCurrent); Assert.Empty(f.PeerSent);
            // Observe B FIRST. Querying A would itself refresh A and mask a missing hook.
            await f.SendPeerAsync(LegacyRuneStoragePacket.Startup().ToFrameBytes());
            Assert.NotEmpty(f.PeerSent);
            Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(f.PeerSent[0]), out var message));
            Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, message.ChatType);
            Assert.Equal("My ranking: " + (success ? "1" : "2") + "; Current rune pack score: 0", message.Content[3]);
            Assert.Equal(peerFiles, File.ReadAllBytes(f.PeerItemsPath));
        }, input: input, ranking: true);
    }
}
