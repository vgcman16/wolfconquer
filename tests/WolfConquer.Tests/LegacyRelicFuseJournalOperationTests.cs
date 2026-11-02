using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyRelicFuseJournalOperationTests
{
    [Fact]
    public void RecipeDataOwnsItsWordsAndBindsTheExactAllocationFields()
    {
        var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), LegacyItemUidRegistry.RelicFuseWriter,
            11, 12, new string('a', 64), 1);
        var receipt = new LegacyItemUidReservationReceipt(Guid.NewGuid(), request.OperationId, request.Writer,
            request.AccountId, request.CharacterUid, request.PayloadSha256, 1, 100, 1);
        uint[] words = [1, 2, 3, 4, 5];
        var recipe = new LegacyRelicFuseJournalOperation(request, receipt, 8, 9, 4_100_001, 1, 3000, words);
        words[0] = 99;
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, recipe.Words);
        Assert.Throws<NotSupportedException>(() => ((IList<uint>)recipe.Words)[0] = 10);
        Assert.Throws<InvalidDataException>(() => new LegacyRelicFuseJournalOperation(request,
            receipt with { PayloadSha256 = new string('b', 64) }, 8, 9, 4_100_001, 1, 3000, words));
        Assert.Throws<InvalidDataException>(() => new LegacyRelicFuseJournalOperation(request,
            receipt, 8, 8, 4_100_001, 1, 3000, words));
        Assert.Throws<InvalidDataException>(() => new LegacyRelicFuseJournalOperation(request,
            receipt, 8, 9, 4_100_001, 1, 3050, words));
    }
}
