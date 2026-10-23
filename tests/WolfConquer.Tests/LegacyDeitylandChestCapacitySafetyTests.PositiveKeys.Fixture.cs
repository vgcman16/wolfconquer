using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(false, false, 1)]
    [InlineData(false, false, 7)]
    [InlineData(false, true, 0)]
    [InlineData(false, true, 1)]
    [InlineData(false, true, 7)]
    [InlineData(true, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(true, false, 7)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, 1)]
    [InlineData(true, true, 7)]
    public Task RegisteredChestDialogOffersUnlockOnlyForActualPositiveKey(
        bool epic, bool hasPositiveKey, byte firstBound) => RunAsync(async fixture =>
    {
        var keyId = epic ? 3_303_974U : 3_303_973U;
        fixture.Seed(40, firstStack: 0, secondStack: hasPositiveKey ? (ushort)1 : (ushort)0, firstBound,
            firstKeyItemId: keyId, secondKeyItemId: keyId, npcId: epic ? 22_607U : 22_598U);
        var before = fixture.Session;

        await fixture.ShowPositiveKeyDialogAsync(epic);

        fixture.AssertUnchanged(before);
        Assert.Equal(hasPositiveKey ? 8 : 7, fixture.Sent.Count);
        for (var index = 0; index < 5; index++)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(fixture.Sent[index]), out var dialog));
            Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
            Assert.Equal(dialog.ToFrameBytes(), fixture.Sent[index]);
        }
        if (hasPositiveKey)
            Assert.Equal(LegacyNpcReplyPacket.ForOption("I~own~the~key!", 1).ToFrameBytes(), fixture.Sent[5]);
        Assert.Equal(LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), fixture.Sent[^2]);
        Assert.Equal(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), fixture.Sent[^1]);
        Assert.DoesNotContain("chest item reward", fixture.Log.ToString());
        Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
    });

    private sealed partial class Fixture
    {
        internal async Task ShowPositiveKeyDialogAsync(bool epic) => await registry.HandleAsync(Context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(epic ? 22_607U : 22_598U,
                optionId: 0, interactType: 1).ToFrameBytes()));

        internal void AssertPositiveKeyCpReward(int keyIndex)
        {
            var updated = Current;
            AssertItems(ExpectedDebit(keyIndex));
            Assert.Equal(Session with
            {
                Character = Session.Character with { ConquerPoints = 200_055 },
                PlayerItems = updated.PlayerItems,
            }, updated);
            Assert.Equal(Session.PlayerItems.InventoryItems.Count -
                (Session.PlayerItems.InventoryItems[keyIndex].StackSize == 1 ? 1 : 0),
                updated.PlayerItems.InventoryItems.Count);
            Assert.Equal(2, Sent.Count);
            AssertDebitFrame(keyIndex);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(Sent[1]), out var update));
            Assert.Equal((uint)Session.Character.Uid, update.Uid);
            Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_055) }, update.Updates);
            AssertCharacterCpSaved();
            Assert.Contains("chest CP reward", Log.ToString());
            Assert.DoesNotContain("inventory is full.", Log.ToString());
            Assert.DoesNotContain("PARITY-GAP", Log.ToString());
        }
    }
}
