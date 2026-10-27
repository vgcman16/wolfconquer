using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("spellNull")]
    [InlineData("spellIdentity")]
    [InlineData("itemNull")]
    [InlineData("itemIdentity")]
    [InlineData("metadataNull")]
    [InlineData("metadataMissing")]
    [InlineData("metadataDuplicate")]
    [InlineData("missingRank")]
    [InlineData("rankNullTail")]
    [InlineData("registryNull")]
    public Task LoadingRejectsCompleteInvalidGraphsBeforeRanksSkillsBadgeOrFrames(string invalid) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedLoadingTransaction(scope);
            switch (invalid)
            {
                case "spellNull": scope.Spells.ClientSpells[2000] = null!; break;
                case "spellIdentity": scope.Spells.ClientSpells[2000] = new() { ID = 2001 }; break;
                case "itemNull": scope.Ninja.Items[999] = null!; break;
                case "itemIdentity": scope.Ninja.Items[999] = scope.Ninja.Items[100]; break;
                case "metadataNull": scope.Metadata[999] = null!; break;
                case "metadataMissing": scope.Metadata.Clear(); break;
                case "metadataDuplicate": scope.Meta(999, 100, 9, 1000, 16310, 16320); break;
                case "missingRank": LegacyNativeNinjaRank.Ranks.Remove(RankType.Fire); break;
                case "rankNullTail": scope.Rank(RankType.Lightning).RankingList[999] = null!; break;
                case "registryNull": LegacyNativeNinjaRank.Ranks = null!; break;
            }
            var before = new LoadingTransactionWitness(scope);

            var error = Record.Exception(() => scope.Ninja.Loading());

            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData("loading")]
    [InlineData("alternate")]
    [InlineData("badge")]
    public Task RequiredPacketPoolAbsenceCannotPublishAnyPreparedNinjaPrefix(string operation) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedLoadingTransaction(scope);
            var before = new LoadingTransactionWitness(scope);
            Exception? error;
            scope.UseNullBin();
            try { error = Record.Exception(() => InvokeLoadingOperation(scope, operation)); }
            finally { scope.RestoreOwnedBin(); }
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });
}
