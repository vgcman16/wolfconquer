using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed class SqlWorldStateModelTests
{
    [Fact]
    public void CompleteImageOwnsInputCopiesAndIncludesEveryDepositedPhysicalUid()
    {
        var character = SqlWorldStateTestData.Character(SqlWorldStateTestData.FirstCharacter);
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(SqlWorldStateTestData.FirstItems());
        var expectedCharacter = character.ToArray(); var expectedItems = items.ToArray();
        Assert.True(SqlWorldStateImage.TryCreate(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.FirstCharacter,
            character, items, out var result, out var failure), failure.ToString());
        var image = Assert.IsType<SqlWorldStateImage>(result);
        Assert.Equal(SqlWorldStateFailure.None, failure);
        Assert.NotEqual(image.AccountId, (long)image.CharacterUid);
        Array.Clear(character); Array.Clear(items);
        var characterCopy = image.CopyCharacterBytes(); Array.Clear(characterCopy);
        var itemCopy = image.CopyItemBytes(); Array.Clear(itemCopy);
        var uidCopy = image.ItemUids.ToArray(); Array.Clear(uidCopy);
        Assert.Equal(expectedCharacter, image.CopyCharacterBytes()); Assert.Equal(expectedItems, image.CopyItemBytes());
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, image.ItemUids.ToArray());
    }

    [Theory]
    [InlineData(0L)] [InlineData(-1L)]
    public void InvalidAccountIdNeverProducesAnOwnedImage(long account)
    {
        Assert.False(SqlWorldStateImage.TryCreate(account, SqlWorldStateTestData.FirstCharacter,
            SqlWorldStateTestData.Character(SqlWorldStateTestData.FirstCharacter), PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty),
            out var image, out var failure));
        Assert.Null(image); Assert.Equal(SqlWorldStateFailure.InvalidAccountId, failure);
    }

    [Fact]
    public void ImageValidationRejectsIdentityWalletAndCompletePhysicalGraphDefectsWithoutChangingInput()
    {
        var validCharacter = SqlWorldStateTestData.Character(SqlWorldStateTestData.FirstCharacter);
        var validItems = PlayerItemSnapshotLoader.ToLegacyBytes(SqlWorldStateTestData.FirstItems());
        var duplicate = SqlWorldStateTestData.FirstItems();
        duplicate = duplicate with { InventoryItems = duplicate.InventoryItems.Append(SqlWorldStateTestData.Item(SqlWorldStateTestData.ChildUid)).ToArray() };
        var cases = new[]
        {
            (0U, validCharacter, validItems, SqlWorldStateFailure.InvalidCharacterUid),
            (9002U, validCharacter, validItems, SqlWorldStateFailure.InvalidCharacterUid),
            (9001U, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(validCharacter).Replace("UID=9001\r\n", "UID=not-a-number\r\n", StringComparison.Ordinal)), validItems, SqlWorldStateFailure.InvalidCharacterImage),
            (9001U, SqlWorldStateTestData.Character(9001, money: -1), validItems, SqlWorldStateFailure.InvalidFunds),
            (9001U, SqlWorldStateTestData.Character(9001, cp: -1), validItems, SqlWorldStateFailure.InvalidFunds),
            (9001U, SqlWorldStateTestData.Character(9001, cp: (long)uint.MaxValue + 1), validItems, SqlWorldStateFailure.InvalidFunds),
            (9001U, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(validCharacter).Replace("WHMoney=5", "WHMoney=5\r\nWarehouseMoney=6", StringComparison.Ordinal)), validItems, SqlWorldStateFailure.InvalidFunds),
            (9001U, validCharacter, validItems.Concat(new byte[] { 0xFF }).ToArray(), SqlWorldStateFailure.InvalidItemImage),
            (9001U, validCharacter, PlayerItemSnapshotLoader.ToLegacyBytes(duplicate), SqlWorldStateFailure.InvalidItemImage)
        };
        foreach (var (uid, character, items, expected) in cases)
        {
            var beforeCharacter = character.ToArray(); var beforeItems = items.ToArray();
            Assert.False(SqlWorldStateImage.TryCreate(SqlWorldStateTestData.FirstAccount, uid, character, items, out var image, out var failure));
            Assert.Null(image); Assert.Equal(expected, failure);
            Assert.Equal(beforeCharacter, character); Assert.Equal(beforeItems, items);
        }
    }

    [Fact]
    public void EmptyAndOneBeyondEachImageBoundRefuseBeforePublishingAnImage()
    {
        var character = SqlWorldStateTestData.Character(SqlWorldStateTestData.FirstCharacter);
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty);
        var tooLarge = new byte[SqlWorldStateImage.MaximumImageBytes + 1];
        foreach (var pair in new[] { (Array.Empty<byte>(), items), (character, Array.Empty<byte>()), (tooLarge, items), (character, tooLarge) })
        {
            Assert.False(SqlWorldStateImage.TryCreate(SqlWorldStateTestData.FirstAccount, SqlWorldStateTestData.FirstCharacter,
                pair.Item1, pair.Item2, out var image, out var failure));
            Assert.Null(image); Assert.Equal(SqlWorldStateFailure.ImageBounds, failure);
        }
    }

    [Fact]
    public void RequestCapturesAndCanonicalizesParticipantsWhileHashingTheCompleteExactImages()
    {
        var initial = SqlWorldStateTestData.Initial(); var id = Guid.NewGuid();
        var input = new SqlWorldStateChange[] { new(initial.Second, 3), new(initial.First, 2) };
        var request = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, id, input);
        var descriptor = request.CopyDescriptorBytes(); var hash = request.CopyDescriptorHash();
        var reordered = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, id,
            new(initial.First, 2), new(initial.Second, 3));
        Assert.Equal(descriptor, reordered.CopyDescriptorBytes()); Assert.Equal(hash, reordered.CopyDescriptorHash());
        Assert.Equal(new long[] { 501, 502 }, request.Changes.ToArray().Select(change => change.Image.AccountId));
        Assert.Equal(SHA256.HashData(descriptor), hash);
        input[0] = new(initial.First, 99);
        var descriptorCopy = request.CopyDescriptorBytes(); Array.Clear(descriptorCopy);
        var hashCopy = request.CopyDescriptorHash(); Array.Clear(hashCopy);
        var changeCopy = request.Changes.ToArray(); changeCopy[0] = new(initial.Second, 99);
        Assert.Equal(descriptor, request.CopyDescriptorBytes()); Assert.Equal(hash, request.CopyDescriptorHash());
        Assert.Equal(2, request.Changes[0].ExpectedRevision); Assert.Equal(3, request.Changes[1].ExpectedRevision);
        var altered = SqlWorldStateTestData.Image(501, 9001, SqlWorldStateTestData.FirstItems(), money: 1001);
        var changedImage = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, id, new(altered, 2), new(initial.Second, 3));
        Assert.False(hash.AsSpan().SequenceEqual(changedImage.CopyDescriptorHash()));
        var changedItems = SqlWorldStateTestData.FirstItems();
        changedItems.InventoryItems[1].PerfectionProgress = 99;
        var alteredItemImage = SqlWorldStateTestData.Image(501, 9001, changedItems);
        Assert.Equal(initial.First.ItemUids.ToArray(), alteredItemImage.ItemUids.ToArray());
        var changedItemRequest = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, id,
            new(alteredItemImage, 2), new(initial.Second, 3));
        Assert.False(hash.AsSpan().SequenceEqual(changedItemRequest.CopyDescriptorHash()));
        var changedRevision = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, id, new(initial.First, 3), new(initial.Second, 3));
        Assert.False(hash.AsSpan().SequenceEqual(changedRevision.CopyDescriptorHash()));
        var changedId = SqlWorldStateTestData.Request(SqlWorldStateOperationKind.SavePair, Guid.NewGuid(), new(initial.First, 2), new(initial.Second, 3));
        Assert.False(hash.AsSpan().SequenceEqual(changedId.CopyDescriptorHash()));
    }

    [Fact]
    public void RequestRejectsInvalidKindRevisionDuplicateParticipantsAndCrossParticipantChildUid()
    {
        var initial = SqlWorldStateTestData.Initial();
        var foreignChild = SqlWorldStateTestData.Image(502, 9002, PlayerItemSnapshot.Empty with
        { InventoryItems = [SqlWorldStateTestData.Item(SqlWorldStateTestData.ChildUid)] });
        var sameCharacter = SqlWorldStateTestData.Image(502, 9001, SqlWorldStateTestData.SecondItems());
        var cases = new[]
        {
            (Guid.Empty, SqlWorldStateOperationKind.Create, new SqlWorldStateChange[] { new(initial.First, 0) }, SqlWorldStateFailure.InvalidOperationId),
            (Guid.NewGuid(), (SqlWorldStateOperationKind)0, new SqlWorldStateChange[] { new(initial.First, 0) }, SqlWorldStateFailure.InvalidOperationKind),
            (Guid.NewGuid(), SqlWorldStateOperationKind.SavePair, new SqlWorldStateChange[] { new(initial.First, 1) }, SqlWorldStateFailure.ParticipantCount),
            (Guid.NewGuid(), SqlWorldStateOperationKind.Create, new SqlWorldStateChange[] { new(initial.First, 1) }, SqlWorldStateFailure.InvalidRevision),
            (Guid.NewGuid(), SqlWorldStateOperationKind.Save, new SqlWorldStateChange[] { new(initial.First, 0) }, SqlWorldStateFailure.InvalidRevision),
            (Guid.NewGuid(), SqlWorldStateOperationKind.Save, new SqlWorldStateChange[] { new(initial.First, long.MaxValue) }, SqlWorldStateFailure.InvalidRevision),
            (Guid.NewGuid(), SqlWorldStateOperationKind.SavePair, new SqlWorldStateChange[] { new(initial.First, 1), new(initial.First, 1) }, SqlWorldStateFailure.ParticipantIdentityConflict),
            (Guid.NewGuid(), SqlWorldStateOperationKind.SavePair, new SqlWorldStateChange[] { new(initial.First, 1), new(sameCharacter, 1) }, SqlWorldStateFailure.ParticipantIdentityConflict),
            (Guid.NewGuid(), SqlWorldStateOperationKind.SavePair, new SqlWorldStateChange[] { new(initial.First, 1), new(foreignChild, 1) }, SqlWorldStateFailure.DuplicateItemUid)
        };
        foreach (var (id, kind, changes, expected) in cases)
        {
            Assert.False(SqlWorldStateRequest.TryCreate(id, kind, changes, out var request, out var failure));
            Assert.Null(request); Assert.Equal(expected, failure);
        }
    }

    [Fact]
    public void ReadRecordCanRepresentLastRevisionButNoNewSaveCanIncrementIt()
    {
        var image = SqlWorldStateTestData.Initial().First;
        Assert.True(SqlWorldStateRecord.TryCreate(image, long.MaxValue, out var result, out var failure));
        var record = Assert.IsType<SqlWorldStateRecord>(result);
        Assert.Equal(SqlWorldStateFailure.None, failure); Assert.Same(image, record.Image); Assert.Equal(long.MaxValue, record.Revision);
        Assert.False(SqlWorldStateRecord.TryCreate(image, 0, out result, out failure));
        Assert.Null(result); Assert.Equal(SqlWorldStateFailure.InvalidRevision, failure);
        Assert.False(SqlWorldStateRecord.TryCreate(null, 1, out result, out failure));
        Assert.Null(result); Assert.Equal(SqlWorldStateFailure.InvalidCharacterImage, failure);
    }
}
